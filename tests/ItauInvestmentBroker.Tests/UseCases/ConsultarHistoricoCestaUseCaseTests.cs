using FluentAssertions;
using ItauInvestmentBroker.Application.UseCases;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using Mapster;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class ConsultarHistoricoCestaUseCaseTests
{
    private readonly ICestaRepository _cestaRepository = Substitute.For<ICestaRepository>();
    private readonly ConsultarHistoricoCestaUseCase _useCase;

    public ConsultarHistoricoCestaUseCaseTests()
    {
        TypeAdapterConfig.GlobalSettings.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible);
        _useCase = new ConsultarHistoricoCestaUseCase(_cestaRepository);
    }

    [Fact]
    public async Task Deve_Retornar_Historico_De_Cestas()
    {
        var cestas = CestaFaker.Criar().Generate(3);
        _cestaRepository.FindAllWithItens(Arg.Any<CancellationToken>()).Returns(cestas.AsEnumerable());

        var result = await _useCase.Executar();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Deve_Retornar_Lista_Vazia_Quando_Nao_Ha_Cestas()
    {
        _cestaRepository.FindAllWithItens(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Cesta>());

        var result = await _useCase.Executar();

        result.Should().BeEmpty();
    }
}
