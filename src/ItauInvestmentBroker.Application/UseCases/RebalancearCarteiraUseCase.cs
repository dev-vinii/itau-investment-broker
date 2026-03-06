using ItauInvestmentBroker.Application.DTOs.Motor;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class RebalancearCarteiraUseCase(
    IClienteRepository clienteRepository,
    ICustodiaRepository custodiaRepository,
    ICotacaoService cotacaoService,
    IKafkaProducer kafkaProducer,
    IUnitOfWork unitOfWork)
{
    private const decimal AliquotaIrVenda = 0.20m;
    private const decimal LimiteIsencaoVendas = 20_000m;
    private const decimal AliquotaIrDedoDuro = 0.00005m;
    private const string TopicoIrDedoDuro = "ir-dedo-duro";
    private const string TopicoIrVenda = "ir-venda";

    public async Task Executar(Cesta cestaAntiga, Cesta cestaNova, CancellationToken cancellationToken = default)
    {
        var clientes = (await clienteRepository.FindAtivos(cancellationToken)).ToList();
        if (clientes.Count == 0)
            return;

        var tickersAntigos = cestaAntiga.Itens.Select(i => i.Ticker).ToHashSet();
        var tickersNovos = cestaNova.Itens.ToDictionary(i => i.Ticker, i => i.Percentual);

        var tickersQueSairam = tickersAntigos.Where(t => !tickersNovos.ContainsKey(t)).ToList();
        var tickersQueEntraram = tickersNovos.Keys.Where(t => !tickersAntigos.Contains(t)).ToList();

        var eventosIrDedoDuro = new List<IrDedoDuroEvent>();
        var eventosIrVenda = new List<IrVendaEvent>();

        foreach (var cliente in clientes)
        {
            var contaGrafica = cliente.ContaGrafica;
            if (contaGrafica is null)
                continue;

            var vendasCliente = new List<VendaInfo>();
            decimal valorObtidoVendas = 0;

            // RN-046/047: Vender toda posição dos ativos que saíram
            foreach (var ticker in tickersQueSairam)
            {
                var resultado = await VenderPosicao(contaGrafica.Id, ticker, cancellationToken);
                if (resultado is null)
                    continue;

                vendasCliente.Add(resultado);
                valorObtidoVendas += resultado.ValorVenda;
            }

            // RN-049: Rebalancear ativos que mudaram de percentual
            var custodias = (await custodiaRepository.FindByContaGraficaId(contaGrafica.Id, cancellationToken))
                .Where(c => c.Quantidade > 0)
                .ToList();

            var valorTotalCarteira = custodias.Sum(c =>
            {
                var cot = cotacaoService.ObterCotacao(c.Ticker);
                return c.Quantidade * (cot?.PrecoFechamento ?? 0);
            }) + valorObtidoVendas;

            foreach (var itemNovo in cestaNova.Itens)
            {
                if (tickersQueEntraram.Contains(itemNovo.Ticker))
                    continue;

                var custodia = custodias.FirstOrDefault(c => c.Ticker == itemNovo.Ticker);
                if (custodia is null || custodia.Quantidade == 0)
                    continue;

                var cotacao = cotacaoService.ObterCotacao(itemNovo.Ticker);
                if (cotacao is null)
                    continue;

                var valorAtual = custodia.Quantidade * cotacao.PrecoFechamento;
                var valorAlvo = valorTotalCarteira * (itemNovo.Percentual / 100m);
                var diferenca = valorAtual - valorAlvo;

                if (diferenca <= cotacao.PrecoFechamento)
                    continue;

                // Vender excesso
                var quantidadeVender = (int)(diferenca / cotacao.PrecoFechamento);
                var valorVenda = quantidadeVender * cotacao.PrecoFechamento;
                var lucro = quantidadeVender * (cotacao.PrecoFechamento - custodia.PrecoMedio);

                vendasCliente.Add(new VendaInfo(itemNovo.Ticker, quantidadeVender, cotacao.PrecoFechamento, custodia.PrecoMedio, lucro, valorVenda));
                valorObtidoVendas += valorVenda;

                // RN-043: Venda não altera PM
                custodia.Quantidade -= quantidadeVender;
                custodia.DataUltimaAtualizacao = DateTime.UtcNow;
            }

            // RN-048: Com o valor obtido, comprar novos ativos proporcionalmente
            if (valorObtidoVendas > 0 && tickersQueEntraram.Count > 0)
            {
                var somaPercentuaisNovos = tickersQueEntraram.Sum(t => tickersNovos[t]);

                foreach (var ticker in tickersQueEntraram)
                {
                    var cotacao = cotacaoService.ObterCotacao(ticker);
                    if (cotacao is null)
                        continue;

                    var proporcao = tickersNovos[ticker] / somaPercentuaisNovos;
                    var valorParaTicker = valorObtidoVendas * proporcao;
                    var quantidade = (int)(valorParaTicker / cotacao.PrecoFechamento);

                    if (quantidade == 0)
                        continue;

                    await ComprarAtivo(contaGrafica.Id, ticker, quantidade, cotacao.PrecoFechamento, cancellationToken);

                    var valorOperacao = quantidade * cotacao.PrecoFechamento;
                    eventosIrDedoDuro.Add(new IrDedoDuroEvent(
                        Tipo: "IR_DEDO_DURO",
                        ClienteId: cliente.Id,
                        Cpf: cliente.Cpf,
                        Ticker: ticker,
                        TipoOperacao: "COMPRA",
                        Quantidade: quantidade,
                        PrecoUnitario: cotacao.PrecoFechamento,
                        ValorOperacao: valorOperacao,
                        Aliquota: AliquotaIrDedoDuro,
                        ValorIR: Math.Round(valorOperacao * AliquotaIrDedoDuro, 2),
                        DataOperacao: DateTime.UtcNow
                    ));
                }
            }

            // RN-057 a 062: Calcular IR sobre vendas
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

        // Publicar eventos Kafka após commit
        foreach (var evento in eventosIrDedoDuro)
        {
            await kafkaProducer.ProduceAsync(TopicoIrDedoDuro, $"{evento.ClienteId}-{evento.Ticker}", evento);
        }

        foreach (var evento in eventosIrVenda)
        {
            await kafkaProducer.ProduceAsync(TopicoIrVenda, evento.ClienteId.ToString(), evento);
        }
    }

    private async Task<VendaInfo?> VenderPosicao(long contaGraficaId, string ticker, CancellationToken cancellationToken)
    {
        var custodia = await custodiaRepository.FindByContaGraficaIdAndTicker(contaGraficaId, ticker, cancellationToken);
        if (custodia is null || custodia.Quantidade == 0)
            return null;

        var cotacao = cotacaoService.ObterCotacao(ticker);
        if (cotacao is null)
            return null;

        var valorVenda = custodia.Quantidade * cotacao.PrecoFechamento;
        var lucro = custodia.Quantidade * (cotacao.PrecoFechamento - custodia.PrecoMedio);
        var resultado = new VendaInfo(ticker, custodia.Quantidade, cotacao.PrecoFechamento, custodia.PrecoMedio, lucro, valorVenda);

        custodia.Quantidade = 0;
        custodia.DataUltimaAtualizacao = DateTime.UtcNow;

        return resultado;
    }

    private async Task ComprarAtivo(long contaGraficaId, string ticker, int quantidade, decimal preco, CancellationToken cancellationToken)
    {
        var custodia = await custodiaRepository.FindByContaGraficaIdAndTicker(contaGraficaId, ticker, cancellationToken);

        if (custodia is null)
        {
            custodiaRepository.Add(new Custodia
            {
                Ticker = ticker,
                Quantidade = quantidade,
                PrecoMedio = preco,
                ContaGraficaId = contaGraficaId,
                DataUltimaAtualizacao = DateTime.UtcNow
            });
        }
        else
        {
            custodia.PrecoMedio =
                (custodia.Quantidade * custodia.PrecoMedio + quantidade * preco)
                / (custodia.Quantidade + quantidade);
            custodia.Quantidade += quantidade;
            custodia.DataUltimaAtualizacao = DateTime.UtcNow;
        }
    }

    private record VendaInfo(string Ticker, int Quantidade, decimal PrecoVenda, decimal PrecoMedio, decimal Lucro, decimal ValorVenda);
}
