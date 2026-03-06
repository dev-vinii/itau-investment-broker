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
        // RN-001/RN-003: Validar dados obrigatorios e valor mensal minimo na adesao.
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        // RN-002: CPF deve ser unico no sistema.
        var clienteExistente = await clienteRepository.FindByCpf(request.Cpf, cancellationToken);
        if (clienteExistente is not null)
            throw new BusinessException(
                $"Já existe um cliente cadastrado com o CPF {request.Cpf}.",
                ErrorCodes.ClienteCpfDuplicado);

        // RN-004: Adesao cria automaticamente a Conta Grafica Filhote.
        var cliente = request.Adapt<Cliente>();
        cliente.ContaGrafica = new ContaGrafica
        {
            NumeroConta = Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Tipo = TipoConta.FILHOTE
        };

        // RN-005/RN-006: Cliente nasce ativo e com data de adesao (defaults da entidade).
        // RN-004 (parcial): custodia filhote e criada sob demanda na primeira distribuicao/compra.
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
