using ItauInvestmentBroker.Application.Features.Cestas.DTOs;
using ItauInvestmentBroker.Application.Common.Exceptions;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
using Mapster;

namespace ItauInvestmentBroker.Application.Features.Cestas.UseCases;

public class ConsultarCestaAtualUseCase(ICestaRepository cestaRepository)
{
    public async Task<CestaResponse> Executar(CancellationToken cancellationToken = default)
    {
        // RN-018: Deve existir apenas uma cesta ativa para consulta da composicao vigente.
        var cesta = await cestaRepository.FindAtiva(cancellationToken)
            ?? throw new NotFoundException(
                "Nenhuma cesta ativa encontrada.",
                ErrorCodes.CestaNaoEncontrada);

        return cesta.Adapt<CestaResponse>();
    }
}
