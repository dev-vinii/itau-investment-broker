using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.UseCases;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class AtualizarValorMensalUseCaseTests
{
    private readonly IClienteRepository _clienteRepository = Substitute.For<IClienteRepository>();
    private readonly IHistoricoValorMensalRepository _historicoRepository = Substitute.For<IHistoricoValorMensalRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IValidator<ValorMensalRequest> _validator = Substitute.For<IValidator<ValorMensalRequest>>();
    private readonly AtualizarValorMensalUseCase _useCase;

    public AtualizarValorMensalUseCaseTests()
    {
        _validator.ValidateAsync(Arg.Any<ValidationContext<ValorMensalRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        _useCase = new AtualizarValorMensalUseCase(_clienteRepository, _historicoRepository, _unitOfWork, _validator);
    }

    [Fact]
    public async Task Deve_Atualizar_Valor_Mensal_E_Registrar_Historico()
    {
        var cliente = ClienteFaker.Criar().Generate();
        var valorAnterior = cliente.ValorMensal;
        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        var request = RequestFaker.ValorMensalRequest().Generate();

        await _useCase.Executar(cliente.Id, request);

        cliente.ValorMensal.Should().Be(request.ValorMensal);
        _historicoRepository.Received(1).Add(Arg.Is<HistoricoValorMensal>(h =>
            h.ClienteId == cliente.Id && h.ValorAnterior == valorAnterior && h.ValorNovo == request.ValorMensal));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Lancar_NotFoundException_Quando_Cliente_Nao_Existe()
    {
        _clienteRepository.FindById(99L, Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        var act = () => _useCase.Executar(99L, RequestFaker.ValorMensalRequest().Generate());

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be(ErrorCodes.ClienteNaoEncontrado);
    }

    [Fact]
    public async Task Deve_Lancar_BusinessException_Quando_Cliente_Inativo()
    {
        var cliente = ClienteFaker.Criar()
            .RuleFor(c => c.Ativo, false)
            .Generate();
        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        var act = () => _useCase.Executar(cliente.Id, RequestFaker.ValorMensalRequest().Generate());

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be(ErrorCodes.ClienteJaInativo);
    }

    [Fact]
    public async Task Deve_Lancar_Quando_Validacao_Falha()
    {
        var realValidator = new Application.Validators.ValorMensalRequestValidator();
        var useCase = new AtualizarValorMensalUseCase(_clienteRepository, _historicoRepository, _unitOfWork, realValidator);

        var act = () => useCase.Executar(1L, new ValorMensalRequest(10m));

        await act.Should().ThrowAsync<ValidationException>();
    }
}
