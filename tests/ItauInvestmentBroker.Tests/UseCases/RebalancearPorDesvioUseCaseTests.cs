using FluentAssertions;
using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Services;
using ItauInvestmentBroker.Application.Features.Clientes.UseCases;
using ItauInvestmentBroker.Application.Features.Cestas.UseCases;
using ItauInvestmentBroker.Application.Features.Motor.UseCases;
using ItauInvestmentBroker.Application.Features.Rentabilidade.UseCases;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class RebalancearPorDesvioUseCaseTests
{
    private readonly ICestaRepository _cestaRepository = Substitute.For<ICestaRepository>();
    private readonly IClienteRepository _clienteRepository = Substitute.For<IClienteRepository>();
    private readonly ICustodiaRepository _custodiaRepository = Substitute.For<ICustodiaRepository>();
    private readonly IVendaRebalanceamentoRepository _vendaRepository = Substitute.For<IVendaRebalanceamentoRepository>();
    private readonly ICotacaoService _cotacaoService = Substitute.For<ICotacaoService>();
    private readonly IKafkaProducer _kafkaProducer = Substitute.For<IKafkaProducer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IOptions<MotorSettings> _motorSettingsOptions = Options.Create(new MotorSettings());
    private readonly CustodiaAppService _custodiaAppService;
    private readonly IrCalculationService _irCalculationService;
    private readonly KafkaEventPublisher _kafkaEventPublisher;
    private readonly RebalancearPorDesvioUseCase _useCase;

    public RebalancearPorDesvioUseCaseTests()
    {
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IDisposable>());
        _custodiaAppService = new CustodiaAppService(_custodiaRepository, _dateTimeProvider);
        _irCalculationService = new IrCalculationService(_vendaRepository, _dateTimeProvider, _motorSettingsOptions);
        _kafkaEventPublisher = new KafkaEventPublisher(_kafkaProducer, Substitute.For<ILogger<KafkaEventPublisher>>(), _motorSettingsOptions);
        _useCase = new RebalancearPorDesvioUseCase(
            _cestaRepository, _clienteRepository, _custodiaRepository,
            _cotacaoService, _custodiaAppService, _irCalculationService,
            _kafkaEventPublisher, _dateTimeProvider, _unitOfWork,
            Substitute.For<ILogger<RebalancearPorDesvioUseCase>>(),
            _motorSettingsOptions);
    }

    private void SetupCestaAtiva()
    {
        var cesta = CestaFaker.CriarComTickers("PETR4", "VALE3");
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns(cesta);
    }

    private void SetupClientes(params Cliente[] clientes)
    {
        _clienteRepository.CountAtivos(Arg.Any<CancellationToken>()).Returns(clientes.Length);
        _clienteRepository.FindAtivosPaginado(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(clientes.ToList());
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
        SetupCestaAtiva();
        _clienteRepository.CountAtivos(Arg.Any<CancellationToken>()).Returns(0);

        var act = () => _useCase.Executar();

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Deve_Nao_Rebalancear_Quando_Desvio_Abaixo_Do_Limiar()
    {
        SetupCestaAtiva();
        var cliente = ClienteFaker.CriarComConta().Generate();
        SetupClientes(cliente);

        // Carteira equilibrada: 50%/50% alvo, precos iguais
        var custodias = new List<Custodia>
        {
            new() { Ticker = "PETR4", Quantidade = 100, PrecoMedio = 10m, ContaGraficaId = cliente.ContaGrafica!.Id },
            new() { Ticker = "VALE3", Quantidade = 100, PrecoMedio = 10m, ContaGraficaId = cliente.ContaGrafica.Id }
        };
        _custodiaRepository.FindByContaGraficaIdsAndTickers(
            Arg.Any<IEnumerable<long>>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(custodias.AsEnumerable());
        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 10m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 10m));

        var result = await _useCase.Executar();

        result.ClientesRebalanceados.Should().Be(0);
        _vendaRepository.DidNotReceive().Add(Arg.Any<VendaRebalanceamento>());
    }

    [Fact]
    public async Task Deve_Rebalancear_Quando_Desvio_Acima_Do_Limiar()
    {
        SetupCestaAtiva();
        var cliente = ClienteFaker.CriarComConta().Generate();
        SetupClientes(cliente);

        // PETR4 tem 80% quando alvo e 50% (desvio = 30 p.p. > 5 p.p.)
        var custodiaPetr = new Custodia { Ticker = "PETR4", Quantidade = 80, PrecoMedio = 10m, ContaGraficaId = cliente.ContaGrafica!.Id };
        var custodiaVale = new Custodia { Ticker = "VALE3", Quantidade = 20, PrecoMedio = 10m, ContaGraficaId = cliente.ContaGrafica.Id };
        _custodiaRepository.FindByContaGraficaIdsAndTickers(
            Arg.Any<IEnumerable<long>>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Custodia> { custodiaPetr, custodiaVale }.AsEnumerable());
        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 10m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 10m));

        _vendaRepository.SomarVendasMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);
        _vendaRepository.SomarLucroMes(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0m);

        _custodiaRepository.FindByContaGraficaIdAndTicker(
            cliente.ContaGrafica.Id, "VALE3", Arg.Any<CancellationToken>())
            .Returns(custodiaVale);

        var result = await _useCase.Executar();

        result.ClientesRebalanceados.Should().Be(1);
        result.LimiarDesvio.Should().Be(5m);
        _vendaRepository.Received().Add(Arg.Is<VendaRebalanceamento>(v => v.Ticker == "PETR4"));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Pular_Cliente_Sem_Custodias()
    {
        SetupCestaAtiva();
        var cliente = ClienteFaker.CriarComConta().Generate();
        SetupClientes(cliente);
        _custodiaRepository.FindByContaGraficaIdsAndTickers(
            Arg.Any<IEnumerable<long>>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Custodia>());

        var result = await _useCase.Executar();

        result.ClientesRebalanceados.Should().Be(0);
    }

    [Fact]
    public async Task Deve_Pular_Cliente_Sem_ContaGrafica()
    {
        SetupCestaAtiva();
        var cliente = ClienteFaker.Criar().Generate(); // sem conta
        SetupClientes(cliente);
        _custodiaRepository.FindByContaGraficaIdsAndTickers(
            Arg.Any<IEnumerable<long>>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Custodia>());

        var result = await _useCase.Executar();

        result.ClientesRebalanceados.Should().Be(0);
    }
}
