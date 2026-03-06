using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;
using ItauInvestmentBroker.Domain.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.UseCases;

public class AdesaoClienteUseCase(
    IClienteRepository clienteRepository,
    IUnitOfWork unitOfWork,
    IValidator<AdesaoRequest> validator)
{
    public async Task<AdesaoResponse> Executar(AdesaoRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var clienteExistente = await clienteRepository.FindByCpf(request.Cpf, cancellationToken);
        if (clienteExistente is not null)
            throw new BusinessException(
                $"Já existe um cliente cadastrado com o CPF {request.Cpf}.",
                ErrorCodes.ClienteCpfDuplicado);

        var cliente = request.Adapt<Cliente>();
        cliente.ContaGrafica = new ContaGrafica
        {
            NumeroConta = Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Tipo = TipoConta.FILHOTE
        };

        clienteRepository.Add(cliente);
        await unitOfWork.CommitAsync(cancellationToken);

        return new AdesaoResponse(
            cliente.Id,
            cliente.Nome,
            cliente.Cpf,
            cliente.Email,
            cliente.ValorMensal,
            cliente.DataAdesao,
            cliente.ContaGrafica.NumeroConta
        );
    }
}
