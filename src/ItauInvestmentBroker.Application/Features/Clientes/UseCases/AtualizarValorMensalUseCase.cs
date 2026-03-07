using FluentValidation;
using ItauInvestmentBroker.Application.Features.Clientes.DTOs;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Domain.Motor.Repositories;

namespace ItauInvestmentBroker.Application.Features.Clientes.UseCases;

public class AtualizarValorMensalUseCase(
    IClienteRepository clienteRepository,
    IHistoricoValorMensalRepository historicoRepository,
    IUnitOfWork unitOfWork,
    IValidator<ValorMensalRequest> validator,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<ValorMensalResponse> Executar(long clienteId, ValorMensalRequest request, CancellationToken cancellationToken = default)
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

        var valorAnterior = cliente.ValorMensal;

        // RN-013: Registrar historico de alteracao de valor mensal.
        historicoRepository.Add(new HistoricoValorMensal
        {
            ClienteId = clienteId,
            ValorAnterior = valorAnterior,
            ValorNovo = request.ValorMensal
        });

        // RN-012: Novo valor passa a ser considerado nas proximas execucoes de compra.
        cliente.ValorMensal = request.ValorMensal;
        await unitOfWork.CommitAsync(cancellationToken);

        return new ValorMensalResponse(
            clienteId,
            valorAnterior,
            request.ValorMensal,
            dateTimeProvider.UtcNow,
            "Valor mensal atualizado. O novo valor sera considerado a partir da proxima data de compra.");
    }
}
