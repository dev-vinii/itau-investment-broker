using FluentValidation;
using ItauInvestmentBroker.Application.DTOs.Cesta;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.UseCases;

public class CriarAtualizarCestaUseCase(
    ICestaRepository cestaRepository,
    IUnitOfWork unitOfWork,
    IValidator<CestaRequest> validator,
    RebalancearCarteiraUseCase rebalancearCarteira)
{
    public async Task<CestaResponse> Executar(CestaRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var cestaAntiga = await cestaRepository.FindAtiva(cancellationToken);
        if (cestaAntiga is not null)
        {
            cestaAntiga.Ativa = false;
            cestaAntiga.DataDesativacao = DateTime.UtcNow;
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