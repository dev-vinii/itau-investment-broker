using FluentAssertions;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Common.Interfaces;
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
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class ConsultarRentabilidadeUseCaseTests
{
    private readonly IClienteRepository _clienteRepository = Substitute.For<IClienteRepository>();
    private readonly IContaGraficaRepository _contaGraficaRepository = Substitute.For<IContaGraficaRepository>();
    private readonly ICustodiaRepository _custodiaRepository = Substitute.For<ICustodiaRepository>();
    private readonly ICotacaoService _cotacaoService = Substitute.For<ICotacaoService>();
    private readonly ConsultarRentabilidadeUseCase _useCase;

    public ConsultarRentabilidadeUseCaseTests()
    {
        _useCase = new ConsultarRentabilidadeUseCase(
            _clienteRepository, _contaGraficaRepository, _custodiaRepository, _cotacaoService);
    }

    [Fact]
    public async Task Deve_Calcular_Rentabilidade_Corretamente()
    {
        var cliente = ClienteFaker.Criar().Generate();
        var contaGrafica = ContaGraficaFaker.CriarFilhote().Generate();
        var custodias = new List<Custodia>
        {
            new() { Ticker = "PETR4", Quantidade = 100, PrecoMedio = 25m, ContaGraficaId = contaGrafica.Id },
            new() { Ticker = "VALE3", Quantidade = 50, PrecoMedio = 60m, ContaGraficaId = contaGrafica.Id }
        };

        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _contaGraficaRepository.FindByClienteId(cliente.Id, Arg.Any<CancellationToken>()).Returns(contaGrafica);
        _custodiaRepository.FindByContaGraficaId(contaGrafica.Id, Arg.Any<CancellationToken>()).Returns(custodias.AsEnumerable());
        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 30m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 70m));

        var result = await _useCase.Executar(cliente.Id);

        // PETR4: investido=100*25=2500, atual=100*30=3000, pl=500
        // VALE3: investido=50*60=3000, atual=50*70=3500, pl=500
        result.ClienteId.Should().Be(cliente.Id);
        result.ValorInvestidoTotal.Should().Be(5500m);
        result.ValorAtualTotal.Should().Be(6500m);
        result.PlTotal.Should().Be(1000m);
        result.RentabilidadePercentual.Should().Be(18.18m);
        result.Ativos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deve_Ignorar_Custodias_Com_Quantidade_Zero()
    {
        var cliente = ClienteFaker.Criar().Generate();
        var contaGrafica = ContaGraficaFaker.CriarFilhote().Generate();
        var custodias = new List<Custodia>
        {
            new() { Ticker = "PETR4", Quantidade = 0, PrecoMedio = 25m, ContaGraficaId = contaGrafica.Id },
            new() { Ticker = "VALE3", Quantidade = 100, PrecoMedio = 60m, ContaGraficaId = contaGrafica.Id }
        };

        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _contaGraficaRepository.FindByClienteId(cliente.Id, Arg.Any<CancellationToken>()).Returns(contaGrafica);
        _custodiaRepository.FindByContaGraficaId(contaGrafica.Id, Arg.Any<CancellationToken>()).Returns(custodias.AsEnumerable());
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 70m));

        var result = await _useCase.Executar(cliente.Id);

        result.Ativos.Should().HaveCount(1);
        result.Ativos[0].Ticker.Should().Be("VALE3");
    }

    [Fact]
    public async Task Deve_Calcular_Composicao_Percentual()
    {
        var cliente = ClienteFaker.Criar().Generate();
        var contaGrafica = ContaGraficaFaker.CriarFilhote().Generate();
        var custodias = new List<Custodia>
        {
            new() { Ticker = "PETR4", Quantidade = 100, PrecoMedio = 10m, ContaGraficaId = contaGrafica.Id },
            new() { Ticker = "VALE3", Quantidade = 100, PrecoMedio = 10m, ContaGraficaId = contaGrafica.Id }
        };

        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _contaGraficaRepository.FindByClienteId(cliente.Id, Arg.Any<CancellationToken>()).Returns(contaGrafica);
        _custodiaRepository.FindByContaGraficaId(contaGrafica.Id, Arg.Any<CancellationToken>()).Returns(custodias.AsEnumerable());
        _cotacaoService.ObterCotacao("PETR4").Returns(CotacaoFaker.Criar("PETR4", 20m));
        _cotacaoService.ObterCotacao("VALE3").Returns(CotacaoFaker.Criar("VALE3", 20m));

        var result = await _useCase.Executar(cliente.Id);

        result.Ativos.Should().AllSatisfy(a => a.ComposicaoPercentual.Should().Be(50m));
    }

    [Fact]
    public async Task Deve_Lancar_NotFoundException_Quando_Cliente_Nao_Existe()
    {
        _clienteRepository.FindById(99L, Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        var act = () => _useCase.Executar(99L);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deve_Lancar_NotFoundException_Quando_ContaGrafica_Nao_Existe()
    {
        var cliente = ClienteFaker.Criar().Generate();
        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _contaGraficaRepository.FindByClienteId(cliente.Id, Arg.Any<CancellationToken>()).Returns((ContaGrafica?)null);

        var act = () => _useCase.Executar(cliente.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deve_Retornar_Rentabilidade_Zero_Quando_Sem_Investimento()
    {
        var cliente = ClienteFaker.Criar().Generate();
        var contaGrafica = ContaGraficaFaker.CriarFilhote().Generate();

        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _contaGraficaRepository.FindByClienteId(cliente.Id, Arg.Any<CancellationToken>()).Returns(contaGrafica);
        _custodiaRepository.FindByContaGraficaId(contaGrafica.Id, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Custodia>());

        var result = await _useCase.Executar(cliente.Id);

        result.ValorInvestidoTotal.Should().Be(0);
        result.ValorAtualTotal.Should().Be(0);
        result.RentabilidadePercentual.Should().Be(0);
        result.Ativos.Should().BeEmpty();
    }
}
