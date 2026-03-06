using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class AtualizarValorMensalUseCase(
    IClienteRepository clienteRepository,
    IUnitOfWork unitOfWork,
    IValidator<ValorMensalRequest> validator)
{
    public async Task Executar(long clienteId, ValorMensalRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var cliente = await clienteRepository.FindById(clienteId, cancellationToken)
            ?? throw new NotFoundException(
                $"Cliente com ID {clienteId} não encontrado.",
                ErrorCodes.ClienteNaoEncontrado);

        if (!cliente.Ativo)
            throw new BusinessException(
                $"Cliente com ID {clienteId} está inativo.",
                ErrorCodes.ClienteJaInativo);

        cliente.ValorMensal = request.ValorMensal;
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
