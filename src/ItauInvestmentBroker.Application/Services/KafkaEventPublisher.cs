using ItauInvestmentBroker.Application.Common.Configuration;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItauInvestmentBroker.Application.Services;

public class KafkaEventPublisher(
    IKafkaProducer kafkaProducer,
    ILogger<KafkaEventPublisher> logger,
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;
    private const int PublishMaxAttempts = 3;
    private const int InitialRetryDelayMs = 500;

    public async Task PublicarEventosIr(
        List<IrDedoDuroEvent> eventosDedoDuro,
        List<IrVendaEvent> eventosVenda)
    {
        foreach (var evento in eventosDedoDuro)
        {
            await PublishWithRetry(
                _settings.TopicoIrDedoDuro,
                $"{evento.ClienteId}-{evento.Ticker}",
                evento,
                $"IR dedo-duro para cliente {evento.ClienteId}, ticker {evento.Ticker}");
        }

        foreach (var evento in eventosVenda)
        {
            await PublishWithRetry(
                _settings.TopicoIrVenda,
                evento.ClienteId.ToString(),
                evento,
                $"IR venda para cliente {evento.ClienteId}");
        }
    }

    public async Task<bool> PublicarOrdemCompraExecutada(OrdemCompraExecutadaEvent evento)
    {
        return await PublishWithRetry(
            _settings.TopicoOrdemCompraExecutada,
            evento.OrdemCompraId.ToString(),
            evento,
            $"ordem de compra executada {evento.OrdemCompraId}");
    }

    public async Task<bool> PublicarMotorExecucaoFalhou(MotorExecucaoFalhouEvent evento)
    {
        return await PublishWithRetry(
            _settings.TopicoMotorExecucaoFalhou,
            $"{evento.Operacao}-{evento.DataOcorrencia:yyyyMMddHHmmssfff}",
            evento,
            $"falha de execucao do motor ({evento.Operacao})");
    }

    private async Task<bool> PublishWithRetry<T>(string topic, string key, T message, string context)
    {
        var attempt = 1;
        var delayMs = InitialRetryDelayMs;

        while (attempt <= PublishMaxAttempts)
        {
            try
            {
                await kafkaProducer.ProduceAsync(topic, key, message);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt == PublishMaxAttempts)
                {
                    logger.LogError(ex,
                        "Falha ao publicar {Context} no topico {Topic} apos {Attempts} tentativas",
                        context, topic, PublishMaxAttempts);
                    return false;
                }

                logger.LogWarning(ex,
                    "Falha ao publicar {Context} no topico {Topic} (tentativa {Attempt}/{Attempts}). Nova tentativa em {DelayMs}ms",
                    context, topic, attempt, PublishMaxAttempts, delayMs);

                await Task.Delay(delayMs);
                delayMs *= 2;
                attempt++;
            }
        }

        return false;
    }
}
