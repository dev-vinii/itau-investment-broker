using ItauInvestmentBroker.Application.Features.Cestas.DTOs;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.Features.Cestas.UseCases;

public class ConsultarHistoricoCestaUseCase(ICestaRepository cestaRepository)
{
    public async Task<List<CestaResponse>> Executar(CancellationToken cancellationToken = default)
    {
        var cestas = await cestaRepository.FindAllWithItens(cancellationToken);

        return cestas.Adapt<List<CestaResponse>>();
    }
}
