using ItauInvestmentBroker.Application.Features.Cestas.DTOs;
using ItauInvestmentBroker.Application.Features.Clientes.UseCases;
using ItauInvestmentBroker.Application.Features.Cestas.UseCases;
using ItauInvestmentBroker.Application.Features.Motor.UseCases;
using ItauInvestmentBroker.Application.Features.Rentabilidade.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ItauInvestmentBroker.API.Features.Cestas.Controllers;

[ApiController]
[Route("admin/cesta")]
public class AdminCestaController(
    CriarAtualizarCestaUseCase criarAtualizarCesta,
    ConsultarCestaAtualUseCase consultarCestaAtual,
    ConsultarHistoricoCestaUseCase consultarHistoricoCesta) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CestaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CestaRequest request, CancellationToken cancellationToken)
    {
        var response = await criarAtualizarCesta.Executar(request, cancellationToken);
        return CreatedAtAction(nameof(Atual), response);
    }

    [HttpGet("atual")]
    [ProducesResponseType(typeof(CestaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atual(CancellationToken cancellationToken)
    {
        var response = await consultarCestaAtual.Executar(cancellationToken);
        return Ok(response);
    }

    [HttpGet("historico")]
    [ProducesResponseType(typeof(List<CestaResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Historico(CancellationToken cancellationToken)
    {
        var response = await consultarHistoricoCesta.Executar(cancellationToken);
        return Ok(response);
    }
}
