using ItauInvestmentBroker.Application.Features.Clientes.DTOs;
using ItauInvestmentBroker.Application.Features.Clientes.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ItauInvestmentBroker.API.Features.Clientes.Controllers;

[ApiController]
[Route("clientes")]
public class ClientesController(
    AdesaoClienteUseCase adesaoCliente,
    SaidaClienteUseCase saidaCliente,
    AtualizarValorMensalUseCase atualizarValorMensal) : ControllerBase
{
    [HttpPost("adesao")]
    [ProducesResponseType(typeof(AdesaoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Adesao([FromBody] AdesaoRequest request, CancellationToken cancellationToken)
    {
        var response = await adesaoCliente.Executar(request, cancellationToken);
        return Created(string.Empty, response);
    }

    [HttpPost("{id:long}/saida")]
    [ProducesResponseType(typeof(SaidaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Saida(long id, CancellationToken cancellationToken)
    {
        var response = await saidaCliente.Executar(id, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:long}/valor-mensal")]
    [ProducesResponseType(typeof(ValorMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarValorMensal(long id, [FromBody] ValorMensalRequest request, CancellationToken cancellationToken)
    {
        var response = await atualizarValorMensal.Executar(id, request, cancellationToken);
        return Ok(response);
    }
}
