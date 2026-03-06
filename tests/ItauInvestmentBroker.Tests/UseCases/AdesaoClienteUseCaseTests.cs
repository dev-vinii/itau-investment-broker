using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using ItauInvestmentBroker.Application.Features.Clientes.DTOs;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Features.Clientes.UseCases;
using ItauInvestmentBroker.Application.Features.Cestas.UseCases;
using ItauInvestmentBroker.Application.Features.Motor.UseCases;
using ItauInvestmentBroker.Application.Features.Rentabilidade.UseCases;
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
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class AdesaoClienteUseCaseTests
{
    private readonly IClienteRepository _clienteRepository = Substitute.For<IClienteRepository>();
    private readonly ICestaRepository _cestaRepository = Substitute.For<ICestaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IValidator<AdesaoRequest> _validator = Substitute.For<IValidator<AdesaoRequest>>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly AdesaoClienteUseCase _useCase;

    public AdesaoClienteUseCaseTests()
    {
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IDisposable>());
        _validator.ValidateAsync(Arg.Any<ValidationContext<AdesaoRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _useCase = new AdesaoClienteUseCase(_clienteRepository, _cestaRepository, _unitOfWork, _validator, _dateTimeProvider);
    }

    [Fact]
    public async Task Deve_Criar_Cliente_Com_ContaGrafica_Filhote()
    {
        var request = RequestFaker.AdesaoRequest().Generate();
        _clienteRepository.FindByCpf(request.Cpf, Arg.Any<CancellationToken>()).Returns((Cliente?)null);
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns((Cesta?)null);

        var result = await _useCase.Executar(request);

        result.Nome.Should().Be(request.Nome);
        result.Cpf.Should().Be(request.Cpf);
        result.Email.Should().Be(request.Email);
        result.ValorMensal.Should().Be(request.ValorMensal);
        result.NumeroConta.Should().NotBeNullOrEmpty();
        _clienteRepository.Received(1).Add(Arg.Is<Cliente>(c =>
            c.ContaGrafica != null && c.ContaGrafica.Tipo == TipoConta.FILHOTE));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Inicializar_Custodias_Com_Ativos_Da_Cesta_Ativa()
    {
        var request = RequestFaker.AdesaoRequest().Generate();
        _clienteRepository.FindByCpf(request.Cpf, Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        var cestaAtiva = CestaFaker.CriarComTickers("PETR4", "VALE3");
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns(cestaAtiva);

        await _useCase.Executar(request);

        _clienteRepository.Received(1).Add(Arg.Is<Cliente>(c =>
            c.ContaGrafica!.Custodias.Count == 2 &&
            c.ContaGrafica.Custodias.All(cu => cu.Quantidade == 0 && cu.PrecoMedio == 0)));
    }

    [Fact]
    public async Task Deve_Lancar_BusinessException_Quando_Cpf_Duplicado()
    {
        var request = RequestFaker.AdesaoRequest().Generate();
        var clienteExistente = ClienteFaker.Criar().Generate();
        clienteExistente.Cpf = request.Cpf;
        _clienteRepository.FindByCpf(request.Cpf, Arg.Any<CancellationToken>()).Returns(clienteExistente);

        var act = () => _useCase.Executar(request);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be(ErrorCodes.ClienteCpfDuplicado);
    }

    [Fact]
    public async Task Deve_Chamar_Validator()
    {
        var request = RequestFaker.AdesaoRequest().Generate();
        _clienteRepository.FindByCpf(request.Cpf, Arg.Any<CancellationToken>()).Returns((Cliente?)null);
        _cestaRepository.FindAtiva(Arg.Any<CancellationToken>()).Returns((Cesta?)null);

        await _useCase.Executar(request);

        await _validator.Received(1).ValidateAsync(
            Arg.Any<ValidationContext<AdesaoRequest>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Lancar_Quando_Validacao_Falha()
    {
        var request = new AdesaoRequest("", "123", "invalido", 10m);
        var realValidator = new ItauInvestmentBroker.Application.Features.Clientes.Validators.AdesaoRequestValidator();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        var useCase = new AdesaoClienteUseCase(_clienteRepository, _cestaRepository, _unitOfWork, realValidator, dateTimeProvider);

        var act = () => useCase.Executar(request);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
