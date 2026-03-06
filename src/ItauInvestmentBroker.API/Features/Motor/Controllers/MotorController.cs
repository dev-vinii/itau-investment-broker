using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Features.Clientes.UseCases;
using ItauInvestmentBroker.Application.Features.Cestas.UseCases;
using ItauInvestmentBroker.Application.Features.Motor.UseCases;
using ItauInvestmentBroker.Application.Features.Rentabilidade.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ItauInvestmentBroker.API.Features.Motor.Controllers;

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
