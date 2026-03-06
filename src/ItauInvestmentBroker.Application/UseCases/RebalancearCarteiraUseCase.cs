using ItauInvestmentBroker.Application.DTOs.Motor;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class RebalancearCarteiraUseCase(
    IClienteRepository clienteRepository,
    ICustodiaRepository custodiaRepository,
    IVendaRebalanceamentoRepository vendaRebalanceamentoRepository,
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
        // RN-045/RN-046: Rebalanceamento por mudanca de cesta para clientes ativos.
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

            // RN-047: Vender toda a posicao dos ativos que sairam da cesta.
            foreach (var ticker in tickersQueSairam)
            {
                var resultado = await VenderPosicao(contaGrafica.Id, ticker, cancellationToken);
                if (resultado is null)
                    continue;

                vendasCliente.Add(resultado);
                valorObtidoVendas += resultado.ValorVenda;
            }

            // RN-049: Rebalancear ativos que mudaram de percentual (vender excesso e comprar deficit).
            var custodias = (await custodiaRepository.FindByContaGraficaId(contaGrafica.Id, cancellationToken))
                .Where(c => c.Quantidade > 0)
                .ToList();

            var valorTotalCarteira = custodias.Sum(c =>
            {
                var cot = cotacaoService.ObterCotacao(c.Ticker);
                return c.Quantidade * (cot?.PrecoFechamento ?? 0);
            }) + valorObtidoVendas;

            var tickersComDeficit = new List<(string Ticker, decimal Deficit, decimal Preco)>();

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

                if (diferenca > cotacao.PrecoFechamento)
                {
                    // Vender excesso
                    var quantidadeVender = (int)(diferenca / cotacao.PrecoFechamento);
                    var valorVenda = quantidadeVender * cotacao.PrecoFechamento;
                    var lucro = quantidadeVender * (cotacao.PrecoFechamento - custodia.PrecoMedio);

                    vendasCliente.Add(new VendaInfo(itemNovo.Ticker, quantidadeVender, cotacao.PrecoFechamento, custodia.PrecoMedio, lucro, valorVenda));
                    valorObtidoVendas += valorVenda;

                    // RN-043: Venda nao altera PM
                    custodia.Quantidade -= quantidadeVender;
                    custodia.DataUltimaAtualizacao = DateTime.UtcNow;
                }
                else if (diferenca < -cotacao.PrecoFechamento)
                {
                    // RN-049: Ativo remanescente com deficit
                    tickersComDeficit.Add((itemNovo.Ticker, Math.Abs(diferenca), cotacao.PrecoFechamento));
                }
            }

            // RN-048: Com o valor obtido nas vendas, comprar novos ativos e ativos com deficit proporcionalmente.
            if (valorObtidoVendas > 0)
            {
                var tickersParaComprar = new List<(string Ticker, decimal Peso, decimal Preco)>();

                // Novos ativos da cesta
                foreach (var ticker in tickersQueEntraram)
                {
                    tickersParaComprar.Add((ticker, tickersNovos[ticker], 0));
                }

                // RN-049: Ativos remanescentes com deficit
                foreach (var (ticker, deficit, preco) in tickersComDeficit)
                {
                    var pesoDeficit = deficit / valorTotalCarteira * 100m;
                    tickersParaComprar.Add((ticker, pesoDeficit, preco));
                }

                if (tickersParaComprar.Count > 0)
                {
                    var somaPesos = tickersParaComprar.Sum(t => t.Peso);

                    foreach (var (ticker, peso, precoCache) in tickersParaComprar)
                    {
                        var cotacao = cotacaoService.ObterCotacao(ticker);
                        var preco = cotacao?.PrecoFechamento ?? precoCache;
                        if (preco == 0) continue;

                        var proporcao = peso / somaPesos;
                        var valorParaTicker = valorObtidoVendas * proporcao;
                        var quantidade = (int)(valorParaTicker / preco);

                        if (quantidade == 0)
                            continue;

                        await ComprarAtivo(contaGrafica.Id, ticker, quantidade, preco, cancellationToken);

                        var valorOperacao = quantidade * preco;
                        eventosIrDedoDuro.Add(new IrDedoDuroEvent(
                            Tipo: "IR_DEDO_DURO",
                            ClienteId: cliente.Id,
                            Cpf: cliente.Cpf,
                            Ticker: ticker,
                            TipoOperacao: "COMPRA",
                            Quantidade: quantidade,
                            PrecoUnitario: preco,
                            ValorOperacao: valorOperacao,
                            Aliquota: AliquotaIrDedoDuro,
                            ValorIR: Math.Round(valorOperacao * AliquotaIrDedoDuro, 2),
                            DataOperacao: DateTime.UtcNow
                        ));
                    }
                }
            }

            // RN-057: Persistir vendas e acumular total mensal para calculo de IR.
            foreach (var venda in vendasCliente)
            {
                vendaRebalanceamentoRepository.Add(new VendaRebalanceamento
                {
                    ClienteId = cliente.Id,
                    Ticker = venda.Ticker,
                    Quantidade = venda.Quantidade,
                    PrecoVenda = venda.PrecoVenda,
                    PrecoMedio = venda.PrecoMedio,
                    ValorVenda = venda.ValorVenda,
                    Lucro = venda.Lucro
                });
            }

            // RN-057 a RN-062: Calcular IR sobre vendas com acumulado mensal.
            if (vendasCliente.Count > 0)
            {
                var agora = DateTime.UtcNow;
                var vendasAnterioresMes = await vendaRebalanceamentoRepository
                    .SomarVendasMes(cliente.Id, agora.Year, agora.Month, cancellationToken);
                var lucroAnteriorMes = await vendaRebalanceamentoRepository
                    .SomarLucroMes(cliente.Id, agora.Year, agora.Month, cancellationToken);

                var totalVendasExecucao = vendasCliente.Sum(v => v.ValorVenda);
                var lucroExecucao = vendasCliente.Sum(v => v.Lucro);

                var totalVendasMes = vendasAnterioresMes + totalVendasExecucao;
                var lucroLiquidoMes = lucroAnteriorMes + lucroExecucao;

                decimal valorIr = 0;
                decimal aliquota = 0;

                if (totalVendasMes > LimiteIsencaoVendas && lucroLiquidoMes > 0)
                {
                    aliquota = AliquotaIrVenda;
                    valorIr = Math.Round(lucroLiquidoMes * AliquotaIrVenda, 2);
                }

                eventosIrVenda.Add(new IrVendaEvent(
                    Tipo: "IR_VENDA",
                    ClienteId: cliente.Id,
                    Cpf: cliente.Cpf,
                    MesReferencia: agora.ToString("yyyy-MM"),
                    TotalVendasMes: Math.Round(totalVendasMes, 2),
                    LucroLiquido: Math.Round(lucroLiquidoMes, 2),
                    Aliquota: aliquota,
                    ValorIR: valorIr,
                    Detalhes: vendasCliente.Select(v => new IrVendaDetalheEvent(
                        v.Ticker, v.Quantidade, v.PrecoVenda, Math.Round(v.PrecoMedio, 2), Math.Round(v.Lucro, 2)
                    )).ToList(),
                    DataCalculo: agora
                ));
            }
        }

        // RN-055/RN-062: Persistir alteracoes antes da publicacao de eventos fiscais no Kafka.
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
