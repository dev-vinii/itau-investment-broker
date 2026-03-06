using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cesta;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.UseCases;

public class CriarAtualizarCestaUseCase(
    ICestaRepository cestaRepository,
    IUnitOfWork unitOfWork,
    IValidator<CestaRequest> validator,
    IDateTimeProvider dateTimeProvider,
    RebalancearCarteiraUseCase rebalancearCarteira)
{
    public async Task<CestaResponse> Executar(CestaRequest request, CancellationToken cancellationToken = default)
    {
        // RN-014/RN-015: Cesta deve ter 5 ativos e somar 100%.
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var cestaAntiga = await cestaRepository.FindAtiva(cancellationToken);
        if (cestaAntiga is not null)
        {
            // RN-017/RN-018: Desativa cesta anterior para manter somente uma cesta ativa.
            cestaAntiga.Ativa = false;
            cestaAntiga.DataDesativacao = dateTimeProvider.UtcNow;
        }

        var novaCesta = request.Adapt<Cesta>();
        cestaRepository.Add(novaCesta);

        await unitOfWork.CommitAsync(cancellationToken);

        // RN-019: Disparar rebalanceamento se havia cesta anterior
        if (cestaAntiga is not null)
        {
            await rebalancearCarteira.Executar(cestaAntiga, novaCesta, cancellationToken);
        }

        return novaCesta.Adapt<CestaResponse>();
    }
}
