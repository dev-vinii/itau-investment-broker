using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cliente;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;
using ItauInvestmentBroker.Domain.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.UseCases;

public class AdesaoClienteUseCase(
    IClienteRepository clienteRepository,
    ICestaRepository cestaRepository,
    IUnitOfWork unitOfWork,
    IValidator<AdesaoRequest> validator,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<AdesaoResponse> Executar(AdesaoRequest request, CancellationToken cancellationToken = default)
    {
        // RN-001/RN-003: Validar dados obrigatorios e valor mensal minimo na adesao.
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        // RN-002: CPF deve ser unico no sistema.
        var clienteExistente = await clienteRepository.FindByCpf(request.Cpf, cancellationToken);
        if (clienteExistente is not null)
            throw new BusinessException(
                $"Ja existe um cliente cadastrado com o CPF {request.Cpf}.",
                ErrorCodes.ClienteCpfDuplicado);

        // RN-004: Adesao cria automaticamente a Conta Grafica Filhote e sua Custodia Filhote.
        var cliente = request.Adapt<Cliente>();
        var contaFilhote = new ContaGrafica
        {
            NumeroConta = Guid.NewGuid().ToString("N")[..10].ToUpper(),
            Tipo = TipoConta.FILHOTE
        };
        cliente.ContaGrafica = contaFilhote;

        // Materializa a custodia filhote na adesao com os ativos da cesta ativa (posicoes zeradas).
        var cestaAtiva = await cestaRepository.FindAtiva(cancellationToken);
        if (cestaAtiva is not null)
        {
            foreach (var item in cestaAtiva.Itens)
            {
                contaFilhote.Custodias.Add(new Custodia
                {
                    Ticker = item.Ticker,
                    Quantidade = 0,
                    PrecoMedio = 0,
                    DataUltimaAtualizacao = dateTimeProvider.UtcNow
                });
            }
        }

        // RN-005/RN-006: Cliente nasce ativo e com data de adesao (defaults da entidade).
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
