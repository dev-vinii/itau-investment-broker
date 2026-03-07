using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Features.Cestas.DTOs;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Features.Cestas.UseCases;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Tests.Fakers;
using Mapster;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class CriarAtualizarCestaUseCaseTests
{
    private readonly ICestaRepository _cestaRepository = Substitute.For<ICestaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IValidator<CestaRequest> _validator = Substitute.For<IValidator<CestaRequest>>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IKafkaProducer _kafkaProducer = Substitute.For<IKafkaProducer>();
    private readonly CriarAtualizarCestaUseCase _useCase;

    public CriarAtualizarCestaUseCaseTests()
    {
        TypeAdapterConfig.GlobalSettings.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible);
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IDisposable>());
        _validator.ValidateAsync(Arg.Any<ValidationContext<CestaRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var motorSettingsOptions = Options.Create(new MotorSettings());
        _useCase = new CriarAtualizarCestaUseCase(
            _cestaRepository, _unitOfWork, _validator, _dateTimeProvider,
            _kafkaProducer, motorSettingsOptions);
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
        await _kafkaProducer.DidNotReceive().ProduceAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RebalanceamentoCarteiraCommand>());
    }

    [Fact]
    public async Task Deve_Desativar_Cesta_Anterior_E_Publicar_Rebalanceamento()
    {
        var cestaAntiga = CestaFaker.Criar().Generate();
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns(cestaAntiga);
        var request = RequestFaker.CestaRequest().Generate();

        await _useCase.Executar(request);

        cestaAntiga.Ativa.Should().BeFalse();
        cestaAntiga.DataDesativacao.Should().NotBeNull();
        await _kafkaProducer.Received(1).ProduceAsync(
            "rebalanceamento-carteira",
            Arg.Any<string>(),
            Arg.Any<RebalanceamentoCarteiraCommand>());
    }

    [Fact]
    public async Task Nao_Deve_Publicar_Rebalanceamento_Quando_Nao_Ha_Cesta_Anterior()
    {
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns((Cesta?)null);
        var request = RequestFaker.CestaRequest().Generate();

        await _useCase.Executar(request);

        await _kafkaProducer.DidNotReceive().ProduceAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RebalanceamentoCarteiraCommand>());
    }
}
