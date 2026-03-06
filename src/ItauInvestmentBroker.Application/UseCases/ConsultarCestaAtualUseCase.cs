using ItauInvestmentBroker.Application.DTOs.Cesta;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Domain.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.UseCases;

public class ConsultarCestaAtualUseCase(ICestaRepository cestaRepository)
{
    public async Task<CestaResponse> Executar(CancellationToken cancellationToken = default)
    {
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
            ?? throw new NotFoundException(
                "Nenhuma cesta ativa encontrada.",
                ErrorCodes.CestaNaoEncontrada);

        return cesta.Adapt<CestaResponse>();
    }
}
