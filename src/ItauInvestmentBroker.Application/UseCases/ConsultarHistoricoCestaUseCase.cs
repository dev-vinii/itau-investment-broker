using ItauInvestmentBroker.Application.DTOs.Cesta;
using ItauInvestmentBroker.Domain.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.UseCases;

public class ConsultarHistoricoCestaUseCase(ICestaRepository cestaRepository)
{
    public async Task<List<CestaResponse>> Executar(CancellationToken cancellationToken = default)
    {
        var cestas = await cestaRepository.FindAllWithItens(cancellationToken);

        return cestas.Adapt<List<CestaResponse>>();
    }
}
