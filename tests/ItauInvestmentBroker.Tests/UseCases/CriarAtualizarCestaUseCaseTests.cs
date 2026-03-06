using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using ItauInvestmentBroker.Application.DTOs.Cesta;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Application.UseCases;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using Mapster;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class CriarAtualizarCestaUseCaseTests
{
    private readonly ICestaRepository _cestaRepository = Substitute.For<ICestaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IValidator<CestaRequest> _validator = Substitute.For<IValidator<CestaRequest>>();
    private readonly RebalancearCarteiraUseCase _rebalancear;
    private readonly CriarAtualizarCestaUseCase _useCase;

    public CriarAtualizarCestaUseCaseTests()
    {
        TypeAdapterConfig.GlobalSettings.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible);
        _validator.ValidateAsync(Arg.Any<ValidationContext<CestaRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var clienteRepo = Substitute.For<IClienteRepository>();
        clienteRepo.FindAtivos(Arg.Any<CancellationToken>()).Returns(Enumerable.Empty<Cliente>());
        _rebalancear = new RebalancearCarteiraUseCase(
            clienteRepo,
            Substitute.For<ICustodiaRepository>(),
            Substitute.For<IVendaRebalanceamentoRepository>(),
            Substitute.For<ICotacaoService>(),
            Substitute.For<IKafkaProducer>(),
            Substitute.For<IUnitOfWork>());
        _useCase = new CriarAtualizarCestaUseCase(_cestaRepository, _unitOfWork, _validator, _rebalancear);
    }

    [Fact]
    public async Task Deve_Criar_Nova_Cesta_Quando_Nao_Ha_Ativa()
    {
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns((Cesta?)null);
        var request = RequestFaker.CestaRequest().Generate();

        var result = await _useCase.Executar(request);

        result.Nome.Should().Be(request.Nome);
        _cestaRepository.Received(1).Add(Arg.Any<Cesta>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Desativar_Cesta_Anterior_E_Disparar_Rebalanceamento()
    {
        var cestaAntiga = CestaFaker.Criar().Generate();
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns(cestaAntiga);
        var request = RequestFaker.CestaRequest().Generate();

        await _useCase.Executar(request);

        cestaAntiga.Ativa.Should().BeFalse();
        cestaAntiga.DataDesativacao.Should().NotBeNull();
    }

    [Fact]
    public async Task Nao_Deve_Disparar_Rebalanceamento_Quando_Nao_Ha_Cesta_Anterior()
    {
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns((Cesta?)null);
        var request = RequestFaker.CestaRequest().Generate();

        await _useCase.Executar(request);

        _cestaRepository.DidNotReceive().Update(Arg.Any<Cesta>());
    }
}
