using ItauInvestmentBroker.Application.DTOs.Motor;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class RebalancearPorDesvioUseCase(
    ICestaRepository cestaRepository,
    IClienteRepository clienteRepository,
    ICustodiaRepository custodiaRepository,
    ICotacaoService cotacaoService,
    IKafkaProducer kafkaProducer,
    IUnitOfWork unitOfWork)
{
    private const decimal LimiarDesvioPontos = 5m;
    private const decimal AliquotaIrVenda = 0.20m;
    private const decimal LimiteIsencaoVendas = 20_000m;
    private const decimal AliquotaIrDedoDuro = 0.00005m;
    private const string TopicoIrDedoDuro = "ir-dedo-duro";
    private const string TopicoIrVenda = "ir-venda";

    public async Task<RebalancearPorDesvioResponse> Executar(CancellationToken cancellationToken = default)
    {
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
            ?? throw new NotFoundException("Nenhuma cesta ativa encontrada.", ErrorCodes.CestaNaoEncontrada);

        var clientes = (await clienteRepository.FindAtivos(cancellationToken)).ToList();
        if (clientes.Count == 0)
            throw new BusinessException("Nenhum cliente ativo encontrado.", ErrorCodes.ClienteNaoEncontrado);

        var percentuaisAlvo = cesta.Itens.ToDictionary(i => i.Ticker, i => i.Percentual);
        var eventosIrDedoDuro = new List<IrDedoDuroEvent>();
        var eventosIrVenda = new List<IrVendaEvent>();
        var clientesRebalanceados = 0;

        foreach (var cliente in clientes)
        {
            var contaGrafica = cliente.ContaGrafica;
            if (contaGrafica is null)
                continue;

            var custodias = (await custodiaRepository.FindByContaGraficaId(contaGrafica.Id, cancellationToken))
                .Where(c => c.Quantidade > 0)
                .ToList();

            if (custodias.Count == 0)
                continue;

            // Calcular valor total da carteira
            var valorTotalCarteira = custodias.Sum(c =>
            {
                var cot = cotacaoService.ObterCotacao(c.Ticker);
                return c.Quantidade * (cot?.PrecoFechamento ?? 0);
            });

            if (valorTotalCarteira == 0)
                continue;

            // RN-051: Verificar desvios
            var desvios = new List<DesvioInfo>();

            foreach (var custodia in custodias)
            {
                if (!percentuaisAlvo.TryGetValue(custodia.Ticker, out var percentualAlvo))
                    continue;

                var cotacao = cotacaoService.ObterCotacao(custodia.Ticker);
                if (cotacao is null)
                    continue;

                var valorAtual = custodia.Quantidade * cotacao.PrecoFechamento;
                var percentualReal = valorAtual / valorTotalCarteira * 100m;
                var desvio = percentualReal - percentualAlvo;

                if (Math.Abs(desvio) >= LimiarDesvioPontos)
                {
                    desvios.Add(new DesvioInfo(
                        custodia.Ticker,
                        percentualReal,
                        percentualAlvo,
                        desvio,
                        cotacao.PrecoFechamento,
                        custodia));
                }
            }

            if (desvios.Count == 0)
                continue;

            clientesRebalanceados++;
            var vendasCliente = new List<VendaInfo>();
            decimal valorDisponivel = 0;

            // RN-052: Vender ativos sobre-alocados
            foreach (var desvio in desvios.Where(d => d.Desvio > 0))
            {
                var valorAlvo = valorTotalCarteira * (desvio.PercentualAlvo / 100m);
                var valorAtual = desvio.Custodia.Quantidade * desvio.PrecoAtual;
                var excesso = valorAtual - valorAlvo;

                var quantidadeVender = (int)(excesso / desvio.PrecoAtual);
                if (quantidadeVender == 0)
                    continue;

                var valorVenda = quantidadeVender * desvio.PrecoAtual;
                var lucro = quantidadeVender * (desvio.PrecoAtual - desvio.Custodia.PrecoMedio);

                vendasCliente.Add(new VendaInfo(desvio.Ticker, quantidadeVender, desvio.PrecoAtual, desvio.Custodia.PrecoMedio, lucro, valorVenda));
                valorDisponivel += valorVenda;

                desvio.Custodia.Quantidade -= quantidadeVender;
                desvio.Custodia.DataUltimaAtualizacao = DateTime.UtcNow;
            }

            // Comprar ativos sub-alocados com o valor obtido
            if (valorDisponivel > 0)
            {
                var subAlocados = desvios.Where(d => d.Desvio < 0).ToList();
                var somaDeficits = subAlocados.Sum(d => Math.Abs(d.Desvio));

                if (somaDeficits > 0)
                {
                    foreach (var desvio in subAlocados)
                    {
                        var proporcao = Math.Abs(desvio.Desvio) / somaDeficits;
                        var valorParaTicker = valorDisponivel * proporcao;
                        var quantidade = (int)(valorParaTicker / desvio.PrecoAtual);

                        if (quantidade == 0)
                            continue;

                        // Atualizar custódia com compra
                        var custodia = desvio.Custodia;
                        custodia.PrecoMedio =
                            (custodia.Quantidade * custodia.PrecoMedio + quantidade * desvio.PrecoAtual)
                            / (custodia.Quantidade + quantidade);
                        custodia.Quantidade += quantidade;
                        custodia.DataUltimaAtualizacao = DateTime.UtcNow;

                        var valorOperacao = quantidade * desvio.PrecoAtual;
                        eventosIrDedoDuro.Add(new IrDedoDuroEvent(
                            Tipo: "IR_DEDO_DURO",
                            ClienteId: cliente.Id,
                            Cpf: cliente.Cpf,
                            Ticker: desvio.Ticker,
                            TipoOperacao: "COMPRA",
                            Quantidade: quantidade,
                            PrecoUnitario: desvio.PrecoAtual,
                            ValorOperacao: valorOperacao,
                            Aliquota: AliquotaIrDedoDuro,
                            ValorIR: Math.Round(valorOperacao * AliquotaIrDedoDuro, 2),
                            DataOperacao: DateTime.UtcNow
                        ));
                    }
                }
            }

            // IR sobre vendas
            if (vendasCliente.Count > 0)
            {
                var totalVendas = vendasCliente.Sum(v => v.ValorVenda);
                var lucroLiquido = vendasCliente.Sum(v => v.Lucro);
                decimal valorIr = 0;
                decimal aliquota = 0;

                if (totalVendas > LimiteIsencaoVendas && lucroLiquido > 0)
                {
                    aliquota = AliquotaIrVenda;
                    valorIr = Math.Round(lucroLiquido * AliquotaIrVenda, 2);
                }

                eventosIrVenda.Add(new IrVendaEvent(
                    Tipo: "IR_VENDA",
                    ClienteId: cliente.Id,
                    Cpf: cliente.Cpf,
                    MesReferencia: DateTime.UtcNow.ToString("yyyy-MM"),
                    TotalVendasMes: Math.Round(totalVendas, 2),
                    LucroLiquido: Math.Round(lucroLiquido, 2),
                    Aliquota: aliquota,
                    ValorIR: valorIr,
                    Detalhes: vendasCliente.Select(v => new IrVendaDetalheEvent(
                        v.Ticker, v.Quantidade, v.PrecoVenda, Math.Round(v.PrecoMedio, 2), Math.Round(v.Lucro, 2)
                    )).ToList(),
                    DataCalculo: DateTime.UtcNow
                ));
            }
        }

        await unitOfWork.CommitAsync(cancellationToken);

        foreach (var evento in eventosIrDedoDuro)
            await kafkaProducer.ProduceAsync(TopicoIrDedoDuro, $"{evento.ClienteId}-{evento.Ticker}", evento);

        foreach (var evento in eventosIrVenda)
            await kafkaProducer.ProduceAsync(TopicoIrVenda, evento.ClienteId.ToString(), evento);

        return new RebalancearPorDesvioResponse(clientesRebalanceados, LimiarDesvioPontos);
    }

    private record DesvioInfo(string Ticker, decimal PercentualReal, decimal PercentualAlvo, decimal Desvio, decimal PrecoAtual, Custodia Custodia);
    private record VendaInfo(string Ticker, int Quantidade, decimal PrecoVenda, decimal PrecoMedio, decimal Lucro, decimal ValorVenda);
}

public record RebalancearPorDesvioResponse(int ClientesRebalanceados, decimal LimiarDesvio);
