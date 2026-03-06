using ItauInvestmentBroker.Application.Features.Rentabilidade.DTOs;
using ItauInvestmentBroker.Application.Features.Clientes.UseCases;
using ItauInvestmentBroker.Application.Features.Cestas.UseCases;
using ItauInvestmentBroker.Application.Features.Motor.UseCases;
using ItauInvestmentBroker.Application.Features.Rentabilidade.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ItauInvestmentBroker.API.Features.Clientes.Controllers;

[ApiController]
[Route("clientes")]
public class RentabilidadeController(ConsultarRentabilidadeUseCase consultarRentabilidade) : ControllerBase
{
    [HttpGet("{id:long}/rentabilidade")]
    [ProducesResponseType(typeof(RentabilidadeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rentabilidade(long id, CancellationToken cancellationToken)
    {
        var response = await consultarRentabilidade.Executar(id, cancellationToken);
        return Ok(response);
    }
}
