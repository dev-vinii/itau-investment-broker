using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class SaidaClienteUseCase(IClienteRepository clienteRepository, IUnitOfWork unitOfWork)
{
    public async Task Executar(long clienteId, CancellationToken cancellationToken = default)
    {
        var cliente = await clienteRepository.FindById(clienteId, cancellationToken)
            ?? throw new NotFoundException(
                $"Cliente com ID {clienteId} não encontrado.",
                ErrorCodes.ClienteNaoEncontrado);

        if (!cliente.Ativo)
            throw new BusinessException(
                $"Cliente com ID {clienteId} já está inativo.",
                ErrorCodes.ClienteJaInativo);

        // RN-007: Saida muda status do cliente para inativo.
        // RN-008: Nao ha venda automatica de custodia no processo de saida.
        cliente.Ativo = false;
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
