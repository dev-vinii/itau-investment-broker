using System.Text.Json;
using FluentValidation;
using ItauInvestmentBroker.Application.Common.Exceptions;

namespace ItauInvestmentBroker.API.Middlewares;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Erro de validacao [CorrelationId={CorrelationId}]", correlationId);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors.Select(e => new { campo = e.PropertyName, mensagem = e.ErrorMessage });
            var response = new { erro = "Erro de validacao.", codigo = "VALIDACAO_INVALIDA", correlationId, detalhes = errors };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "Recurso nao encontrado: {Codigo} [CorrelationId={CorrelationId}]", ex.Codigo, correlationId);
            await WriteResponse(context, StatusCodes.Status404NotFound, ex.Message, ex.Codigo, correlationId);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Erro de negocio: {Codigo} [CorrelationId={CorrelationId}]", ex.Codigo, correlationId);
            await WriteResponse(context, StatusCodes.Status400BadRequest, ex.Message, ex.Codigo, correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro inesperado [CorrelationId={CorrelationId}]", correlationId);
            await WriteResponse(context, StatusCodes.Status500InternalServerError, "Erro interno do servidor.", "ERRO_INTERNO", correlationId);
        }
    }

    private static async Task WriteResponse(HttpContext context, int statusCode, string erro, string codigo, string correlationId)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var response = new { erro, codigo, correlationId };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
