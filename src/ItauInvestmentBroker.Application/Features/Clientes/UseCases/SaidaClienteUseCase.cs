using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Application.Features.Clientes.DTOs;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;

namespace ItauInvestmentBroker.Application.Features.Clientes.UseCases;

public class SaidaClienteUseCase(IClienteRepository clienteRepository, IUnitOfWork unitOfWork)
{
    public async Task<SaidaResponse> Executar(long clienteId, CancellationToken cancellationToken = default)
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

        return new SaidaResponse(
            cliente.Id,
            cliente.Nome,
            cliente.Cpf,
            cliente.Email,
            cliente.DataAdesao,
            DateTime.UtcNow
        );
    }
}
