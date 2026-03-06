using FluentAssertions;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Application.UseCases;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class ExecutarCompraUseCaseTests
{
    private readonly ICestaRepository _cestaRepository = Substitute.For<ICestaRepository>();
    private readonly IClienteRepository _clienteRepository = Substitute.For<IClienteRepository>();
    private readonly IContaGraficaRepository _contaGraficaRepository = Substitute.For<IContaGraficaRepository>();
    private readonly ICustodiaRepository _custodiaRepository = Substitute.For<ICustodiaRepository>();
    private readonly IOrdemCompraRepository _ordemCompraRepository = Substitute.For<IOrdemCompraRepository>();
    private readonly IDistribuicaoRepository _distribuicaoRepository = Substitute.For<IDistribuicaoRepository>();
    private readonly ICotacaoService _cotacaoService = Substitute.For<ICotacaoService>();
    private readonly IKafkaProducer _kafkaProducer = Substitute.For<IKafkaProducer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ExecutarCompraUseCase _useCase;

    public ExecutarCompraUseCaseTests()
    {
        _useCase = new ExecutarCompraUseCase(
            _cestaRepository, _clienteRepository, _contaGraficaRepository,
            _custodiaRepository, _ordemCompraRepository, _distribuicaoRepository,
            _cotacaoService, _kafkaProducer, _unitOfWork);
    }

    private Cesta SetupCestaAtiva(params (string ticker, decimal percentual)[] itens)
    {
        var cesta = new Cesta
        {
            Id = 1, Nome = "Cesta", Ativa = true,
            Itens = itens.Select(i => new ItemCesta { Ticker = i.ticker, Percentual = i.percentual }).ToList()
        };
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns(cesta);
        return cesta;
    }

    private ContaGrafica SetupContaMaster()
    {
        var contaMaster = ContaGraficaFaker.CriarMaster().Generate();
        _contaGraficaRepository.FindMaster(Arg.Any<CancellationToken>())
            .Returns(new List<ContaGrafica> { contaMaster }.AsEnumerable());
        return contaMaster;
    }

    private List<Cliente> SetupClientes(int quantidade = 1)
    {
        var clientes = ClienteFaker.CriarComConta().Generate(quantidade);
        _clienteRepository.FindAtivos(Arg.Any<CancellationToken>()).Returns(clientes.AsEnumerable());
        return clientes;
    }

    [Fact]
    public async Task Deve_Lancar_NotFoundException_Quando_Sem_Cesta_Ativa()
    {
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns((Cesta?)null);

        var act = () => _useCase.Executar();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deve_Lancar_BusinessException_Quando_Sem_Clientes_Ativos()
    {
        SetupCestaAtiva(("PETR4", 50), ("VALE3", 50));
        _clienteRepository.FindAtivos(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Cliente>());

        var act = () => _useCase.Executar();

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Deve_Lancar_Quando_Conta_Master_Nao_Encontrada()
    {
        SetupCestaAtiva(("PETR4", 50), ("VALE3", 50));
        SetupClientes();
        _contaGraficaRepository.FindMaster(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<ContaGrafica>());

        var act = () => _useCase.Executar();

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deve_Lancar_Quando_Cotacao_Nao_Encontrada()
    {
        SetupCestaAtiva(("PETR4", 50), ("VALE3", 50));
        SetupClientes();
        SetupContaMaster();
        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 30m));
        _cotacaoService.ObterCotacao("VALE3").Returns(default(Application.Models.Cotacao));

        // PETR4 works but VALE3 will fail - however order depends on Itens iteration
        // Since PETR4 has cotacao, it won't fail on PETR4. VALE3 has null cotacao.
        var act = () => _useCase.Executar();

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be(ErrorCodes.CotacaoNaoEncontrada);
    }

    [Fact]
    public async Task Deve_Executar_Compra_E_Distribuir_Para_Clientes()
    {
        SetupCestaAtiva(("PETR4", 50), ("VALE3", 50));
        var clientes = SetupClientes();
        SetupContaMaster();

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 30m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 60m));
        _custodiaRepository.FindByContaGraficaIdAndTicker(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Custodia?)null);

        var result = await _useCase.Executar();

        result.Should().NotBeNull();
        result.TotalClientes.Should().Be(1);
        _ordemCompraRepository.Received(1).Add(Arg.Any<OrdemCompra>());
        _distribuicaoRepository.Received(1).Add(Arg.Any<Distribuicao>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Separar_Mercado_Lote_E_Fracionario()
    {
        SetupCestaAtiva(("PETR4", 100));
        var clientes = SetupClientes();
        clientes[0].ValorMensal = 300m;
        var contaMaster = SetupContaMaster();

        // aporte=100, ticker 100% => R$100 / R$0.50 = 200 unidades = 200 lote + 0 frac
        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 0.50m));
        _custodiaRepository.FindByContaGraficaIdAndTicker(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Custodia?)null);

        var result = await _useCase.Executar();

        result.Itens.Should().HaveCount(1);
        result.Itens[0].QuantidadeLote.Should().Be(200);
        result.Itens[0].QuantidadeFracionario.Should().Be(0);
    }

    [Fact]
    public async Task Deve_Publicar_Eventos_Ir_DedoDuro_No_Kafka()
    {
        SetupCestaAtiva(("PETR4", 50), ("VALE3", 50));
        SetupClientes();
        SetupContaMaster();

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 10m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 10m));
        _custodiaRepository.FindByContaGraficaIdAndTicker(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Custodia?)null);

        await _useCase.Executar();

        await _kafkaProducer.Received().ProduceAsync(
            "ir-dedo-duro", Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Deve_Descontar_Saldo_Master_Da_Quantidade_A_Comprar()
    {
        SetupCestaAtiva(("PETR4", 100));
        var clientes = SetupClientes();
        clientes[0].ValorMensal = 300m;
        var contaMaster = SetupContaMaster();

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 10m));

        // Saldo master de 5 unidades
        var custodiaMaster = new Custodia { Ticker = "PETR4", Quantidade = 5, PrecoMedio = 9m, ContaGraficaId = contaMaster.Id };
        _custodiaRepository.FindByContaGraficaIdAndTicker(contaMaster.Id, "PETR4", Arg.Any<CancellationToken>())
            .Returns(custodiaMaster);
        _custodiaRepository.FindByContaGraficaIdAndTicker(Arg.Is<long>(id => id != contaMaster.Id), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Custodia?)null);

        var result = await _useCase.Executar();

        // aporte=100, valor ticker=100, qtd necessaria=10, saldo master=5, qtd comprar=5
        result.Itens[0].QuantidadeTotal.Should().Be(5);
    }
}
