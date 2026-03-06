using FluentAssertions;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.UseCases;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using Mapster;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class ConsultarCestaAtualUseCaseTests
{
    private readonly ICestaRepository _cestaRepository = Substitute.For<ICestaRepository>();
    private readonly ConsultarCestaAtualUseCase _useCase;

    public ConsultarCestaAtualUseCaseTests()
    {
        TypeAdapterConfig.GlobalSettings.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible);
        _useCase = new ConsultarCestaAtualUseCase(_cestaRepository);
    }

    [Fact]
    public async Task Deve_Retornar_Cesta_Ativa()
    {
        var cesta = CestaFaker.Criar().Generate();
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns(cesta);

        var result = await _useCase.Executar();

        result.Should().NotBeNull();
        result.Nome.Should().Be(cesta.Nome);
    }

    [Fact]
    public async Task Deve_Lancar_NotFoundException_Quando_Nao_Ha_Cesta_Ativa()
    {
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns((Cesta?)null);

        var act = () => _useCase.Executar();

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be(ErrorCodes.CestaNaoEncontrada);
    }
}
