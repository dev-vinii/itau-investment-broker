using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class AtualizarValorMensalUseCase(
    IClienteRepository clienteRepository,
    IHistoricoValorMensalRepository historicoRepository,
    IUnitOfWork unitOfWork,
    IValidator<ValorMensalRequest> validator)
{
    public async Task Executar(long clienteId, ValorMensalRequest request, CancellationToken cancellationToken = default)
    {
        // RN-011: Cliente pode alterar valor mensal a qualquer momento (com validacao de minimo).
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var cliente = await clienteRepository.FindById(clienteId, cancellationToken)
            ?? throw new NotFoundException(
                $"Cliente com ID {clienteId} não encontrado.",
                ErrorCodes.ClienteNaoEncontrado);

        if (!cliente.Ativo)
            throw new BusinessException(
                $"Cliente com ID {clienteId} está inativo.",
                ErrorCodes.ClienteJaInativo);

        // RN-013: Registrar historico de alteracao de valor mensal.
        historicoRepository.Add(new HistoricoValorMensal
        {
            ClienteId = clienteId,
            ValorAnterior = cliente.ValorMensal,
            ValorNovo = request.ValorMensal
        });

        // RN-012: Novo valor passa a ser considerado nas proximas execucoes de compra.
        cliente.ValorMensal = request.ValorMensal;
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
