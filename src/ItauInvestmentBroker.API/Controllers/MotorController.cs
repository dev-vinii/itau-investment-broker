using ItauInvestmentBroker.Application.DTOs.Motor;
using ItauInvestmentBroker.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ItauInvestmentBroker.API.Controllers;

[ApiController]
[Route("motor")]
public class MotorController(
    ExecutarCompraUseCase executarCompra,
    RebalancearPorDesvioUseCase rebalancearPorDesvio) : ControllerBase
{
    [HttpPost("executar-compra")]
    [ProducesResponseType(typeof(ExecutarCompraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecutarCompra(CancellationToken cancellationToken)
    {
        var response = await executarCompra.Executar(cancellationToken);
        return Ok(response);
    }

    [HttpPost("rebalancear-desvio")]
    [ProducesResponseType(typeof(RebalancearPorDesvioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RebalancearPorDesvio(CancellationToken cancellationToken)
    {
        var response = await rebalancearPorDesvio.Executar(cancellationToken);
        return Ok(response);
    }
}
