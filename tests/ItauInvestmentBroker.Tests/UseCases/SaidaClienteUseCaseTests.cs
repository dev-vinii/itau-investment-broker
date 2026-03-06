using FluentAssertions;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.UseCases;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Tests.Fakers;
using NSubstitute;

namespace ItauInvestmentBroker.Tests.UseCases;

public class SaidaClienteUseCaseTests
{
    private readonly IClienteRepository _clienteRepository = Substitute.For<IClienteRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SaidaClienteUseCase _useCase;

    public SaidaClienteUseCaseTests()
    {
        _useCase = new SaidaClienteUseCase(_clienteRepository, _unitOfWork);
    }

    [Fact]
    public async Task Deve_Inativar_Cliente_Ativo()
    {
        var cliente = ClienteFaker.Criar().Generate();
        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        await _useCase.Executar(cliente.Id);

        cliente.Ativo.Should().BeFalse();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_Lancar_NotFoundException_Quando_Cliente_Nao_Existe()
    {
        _clienteRepository.FindById(99L, Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        var act = () => _useCase.Executar(99L);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be(ErrorCodes.ClienteNaoEncontrado);
    }

    [Fact]
    public async Task Deve_Lancar_BusinessException_Quando_Cliente_Ja_Inativo()
    {
        var cliente = ClienteFaker.Criar()
            .RuleFor(c => c.Ativo, false)
            .Generate();
        _clienteRepository.FindById(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        var act = () => _useCase.Executar(cliente.Id);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be(ErrorCodes.ClienteJaInativo);
    }
}
