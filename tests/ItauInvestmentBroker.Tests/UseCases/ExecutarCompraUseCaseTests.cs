using FluentAssertions;
using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Services;
using ItauInvestmentBroker.Application.Features.Motor.UseCases;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Clientes.Enums;
using ItauInvestmentBroker.Domain.Motor.Enums;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IVendaRebalanceamentoRepository _vendaRebalanceamentoRepository = Substitute.For<IVendaRebalanceamentoRepository>();
    private readonly IOptions<MotorSettings> _motorSettingsOptions = Options.Create(new MotorSettings());
    private readonly CustodiaAppService _custodiaAppService;
    private readonly IrCalculationService _irCalculationService;
    private readonly KafkaEventPublisher _kafkaEventPublisher;
    private readonly ExecutarCompraUseCase _useCase;

    public ExecutarCompraUseCaseTests()
    {
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IDisposable>());
        _custodiaRepository.FindByContaGraficaIdsAndTickers(Arg.Any<IEnumerable<long>>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Custodia>());
        _custodiaAppService = new CustodiaAppService(_custodiaRepository, _dateTimeProvider);
        _irCalculationService = new IrCalculationService(_vendaRebalanceamentoRepository, _dateTimeProvider, _motorSettingsOptions);
        _kafkaEventPublisher = new KafkaEventPublisher(_kafkaProducer, Substitute.For<ILogger<KafkaEventPublisher>>(), _motorSettingsOptions);
        _useCase = new ExecutarCompraUseCase(
            _cestaRepository, _clienteRepository, _contaGraficaRepository,
            _custodiaRepository, _ordemCompraRepository, _distribuicaoRepository,
            _cotacaoService, _irCalculationService,
            _kafkaEventPublisher, Substitute.For<ILogger<ExecutarCompraUseCase>>(), _dateTimeProvider, _unitOfWork, _motorSettingsOptions);
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
        var somaValorMensal = clientes.Sum(c => c.ValorMensal);
        _clienteRepository.CountAtivos(Arg.Any<CancellationToken>()).Returns(clientes.Count);
        _clienteRepository.SomarValorMensalAtivos(Arg.Any<CancellationToken>()).Returns(somaValorMensal);
        _clienteRepository.FindAtivosPaginado(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(clientes);
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
        _clienteRepository.CountAtivos(Arg.Any<CancellationToken>()).Returns(0);

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
        _cotacaoService.ObterCotacao("VALE3").Returns(default(ItauInvestmentBroker.Application.Common.Models.Cotacao));

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
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Separar_Mercado_Lote_E_Fracionario()
    {
        SetupCestaAtiva(("PETR4", 100));
        var clientes = SetupClientes();
        clientes[0].ValorMensal = 300m;
        _clienteRepository.SomarValorMensalAtivos(Arg.Any<CancellationToken>()).Returns(300m);
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
            KafkaTopicNames.IrDedoDuro, Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Deve_Publicar_Evento_Ordem_Compra_Executada_No_Kafka()
    {
        SetupCestaAtiva(("PETR4", 50), ("VALE3", 50));
        SetupClientes();
        SetupContaMaster();

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 10m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 10m));

        await _useCase.Executar();

        await _kafkaProducer.Received().ProduceAsync(
            KafkaTopicNames.OrdemCompraExecutada, Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Deve_Publicar_Evento_Motor_Execucao_Falhou_No_Kafka_Quando_Erro_No_Fluxo()
    {
        SetupCestaAtiva(("PETR4", 100));
        SetupClientes();
        SetupContaMaster();
        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 10m));
        _distribuicaoRepository.When(x => x.Add(Arg.Any<Distribuicao>())).Do(_ => throw new InvalidOperationException("Falha simulada"));

        var act = () => _useCase.Executar();

        await act.Should().ThrowAsync<InvalidOperationException>();

        await _kafkaProducer.Received().ProduceAsync(
            KafkaTopicNames.MotorExecucaoFalhou, Arg.Any<string>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Deve_Descontar_Saldo_Master_Da_Quantidade_A_Comprar()
    {
        SetupCestaAtiva(("PETR4", 100));
        var clientes = SetupClientes();
        clientes[0].ValorMensal = 300m;
        _clienteRepository.SomarValorMensalAtivos(Arg.Any<CancellationToken>()).Returns(300m);
        var contaMaster = SetupContaMaster();

        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 10m));

        // Saldo master de 5 unidades
        var custodiaMaster = new Custodia { Ticker = "PETR4", Quantidade = 5, PrecoMedio = 9m, ContaGraficaId = contaMaster.Id };
        _custodiaRepository.FindByContaGraficaIdsAndTickers(
                Arg.Is<IEnumerable<long>>(ids => ids.Contains(contaMaster.Id)),
                Arg.Is<IEnumerable<string>>(tickers => tickers.Contains("PETR4")),
                Arg.Any<CancellationToken>())
            .Returns([custodiaMaster]);

        var result = await _useCase.Executar();

        // aporte=100, valor ticker=100, qtd necessaria=10, saldo master=5, qtd comprar=5
        result.Itens[0].QuantidadeTotal.Should().Be(5);
    }
}
