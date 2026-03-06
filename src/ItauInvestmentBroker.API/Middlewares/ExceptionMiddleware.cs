using System.Text.Json;
using FluentValidation;
using ItauInvestmentBroker.Application.Exceptions;

namespace ItauInvestmentBroker.API.Middlewares;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Erro de validação");

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors.Select(e => new { campo = e.PropertyName, mensagem = e.ErrorMessage });
            var response = new { erro = "Erro de validação.", codigo = "VALIDACAO_INVALIDA", detalhes = errors };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "Recurso não encontrado: {Codigo}", ex.Codigo);
            await WriteResponse(context, StatusCodes.Status404NotFound, ex.Message, ex.Codigo);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Erro de negócio: {Codigo}", ex.Codigo);
            await WriteResponse(context, StatusCodes.Status400BadRequest, ex.Message, ex.Codigo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro inesperado");
            await WriteResponse(context, StatusCodes.Status500InternalServerError, "Erro interno do servidor.", "ERRO_INTERNO");
        }
    }

    private static async Task WriteResponse(HttpContext context, int statusCode, string erro, string codigo)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var response = new { erro, codigo };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
