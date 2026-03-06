using FluentAssertions;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Application.UseCases;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class RebalancearCarteiraUseCaseTests
{
    private readonly IClienteRepository _clienteRepository = Substitute.For<IClienteRepository>();
    private readonly ICustodiaRepository _custodiaRepository = Substitute.For<ICustodiaRepository>();
    private readonly IVendaRebalanceamentoRepository _vendaRepository = Substitute.For<IVendaRebalanceamentoRepository>();
    private readonly ICotacaoService _cotacaoService = Substitute.For<ICotacaoService>();
    private readonly IKafkaProducer _kafkaProducer = Substitute.For<IKafkaProducer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RebalancearCarteiraUseCase _useCase;

    public RebalancearCarteiraUseCaseTests()
    {
        _useCase = new RebalancearCarteiraUseCase(
            _clienteRepository, _custodiaRepository, _vendaRepository,
            _cotacaoService, _kafkaProducer, _unitOfWork);
    }

    [Fact]
    public async Task Deve_Retornar_Sem_Acao_Quando_Sem_Clientes_Ativos()
    {
        _clienteRepository.FindAtivos(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Cliente>());

        await _useCase.Executar(
            CestaFaker.CriarComTickers("PETR4", "VALE3"),
            CestaFaker.CriarComTickers("ITUB4", "BBDC4"));

        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Vender_Posicao_De_Ativos_Que_Sairam_Da_Cesta()
    {
        var cliente = ClienteFaker.CriarComConta().Generate();
        var contaId = cliente.ContaGrafica!.Id;
        _clienteRepository.FindAtivos(Arg.Any<CancellationToken>())
            .Returns(new List<Cliente> { cliente }.AsEnumerable());

        var custodiaPetr = new Custodia { Ticker = "PETR4", Quantidade = 100, PrecoMedio = 25m, ContaGraficaId = contaId };
        _custodiaRepository.FindByContaGraficaIdAndTicker(contaId, "PETR4", Arg.Any<CancellationToken>()).Returns(custodiaPetr);
        _custodiaRepository.FindByContaGraficaId(contaId, Arg.Any<CancellationToken>())
            .Returns(new List<Custodia> { new() { Ticker = "VALE3", Quantidade = 50, PrecoMedio = 60m } }.AsEnumerable());

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 30m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 70m));
        _cotacaoService.ObterCotacao("ITUB4").Returns(CotacaoFaker.Criar("ITUB4", 25m));

        _vendaRepository.SomarVendasMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);
        _vendaRepository.SomarLucroMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);

        await _useCase.Executar(
            CestaFaker.CriarComTickers("PETR4", "VALE3"),
            CestaFaker.CriarComTickers("VALE3", "ITUB4"));

        custodiaPetr.Quantidade.Should().Be(0);
        _vendaRepository.Received().Add(Arg.Is<VendaRebalanceamento>(v => v.Ticker == "PETR4"));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Calcular_Ir_Venda_Quando_Acima_De_20k()
    {
        var cliente = ClienteFaker.CriarComConta().Generate();
        var contaId = cliente.ContaGrafica!.Id;
        _clienteRepository.FindAtivos(Arg.Any<CancellationToken>())
            .Returns(new List<Cliente> { cliente }.AsEnumerable());

        // Venda grande: 1000 * 30 = R$30.000 > R$20.000
        var custodiaPetr = new Custodia { Ticker = "PETR4", Quantidade = 1000, PrecoMedio = 25m, ContaGraficaId = contaId };
        _custodiaRepository.FindByContaGraficaIdAndTicker(contaId, "PETR4", Arg.Any<CancellationToken>()).Returns(custodiaPetr);
        _custodiaRepository.FindByContaGraficaId(contaId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Custodia>());

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 30m));
        _cotacaoService.ObterCotacao("ITUB4").Returns(CotacaoFaker.Criar("ITUB4", 25m));

        _vendaRepository.SomarVendasMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);
        _vendaRepository.SomarLucroMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);

        await _useCase.Executar(
            CestaFaker.CriarComTickers("PETR4"),
            CestaFaker.CriarComTickers("ITUB4"));

        await _kafkaProducer.Received().ProduceAsync("ir-venda", Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Nao_Deve_Cobrar_Ir_Quando_Vendas_Abaixo_De_20k()
    {
        var cliente = ClienteFaker.CriarComConta().Generate();
        var contaId = cliente.ContaGrafica!.Id;
        _clienteRepository.FindAtivos(Arg.Any<CancellationToken>())
            .Returns(new List<Cliente> { cliente }.AsEnumerable());

        // Venda pequena: 10 * 30 = R$300 < R$20.000
        var custodiaPetr = new Custodia { Ticker = "PETR4", Quantidade = 10, PrecoMedio = 25m, ContaGraficaId = contaId };
        _custodiaRepository.FindByContaGraficaIdAndTicker(contaId, "PETR4", Arg.Any<CancellationToken>()).Returns(custodiaPetr);
        _custodiaRepository.FindByContaGraficaId(contaId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Custodia>());

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 30m));
        _cotacaoService.ObterCotacao("ITUB4").Returns(CotacaoFaker.Criar("ITUB4", 25m));

        _vendaRepository.SomarVendasMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);
        _vendaRepository.SomarLucroMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);

        await _useCase.Executar(
            CestaFaker.CriarComTickers("PETR4"),
            CestaFaker.CriarComTickers("ITUB4"));

        // Evento é publicado mas com ValorIR = 0
        await _kafkaProducer.Received().ProduceAsync("ir-venda", Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Deve_Pular_Cliente_Sem_ContaGrafica()
    {
        var cliente = ClienteFaker.Criar().Generate(); // sem conta
        _clienteRepository.FindAtivos(Arg.Any<CancellationToken>())
            .Returns(new List<Cliente> { cliente }.AsEnumerable());

        await _useCase.Executar(
            CestaFaker.CriarComTickers("PETR4"),
            CestaFaker.CriarComTickers("ITUB4"));

        _vendaRepository.DidNotReceive().Add(Arg.Any<VendaRebalanceamento>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
