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

    public async Task PublicarEventosIr(
        List<IrDedoDuroEvent> eventosDedoDuro,
        List<IrVendaEvent> eventosVenda)
    {
        foreach (var evento in eventosDedoDuro)
        {
            try
            {
                await kafkaProducer.ProduceAsync(
                    _settings.TopicoIrDedoDuro,
                    $"{evento.ClienteId}-{evento.Ticker}",
                    evento);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Falha ao publicar evento IR dedo-duro para cliente {ClienteId}, ticker {Ticker}",
                    evento.ClienteId, evento.Ticker);
            }
        }

        foreach (var evento in eventosVenda)
        {
            try
            {
                await kafkaProducer.ProduceAsync(
                    _settings.TopicoIrVenda,
                    evento.ClienteId.ToString(),
                    evento);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Falha ao publicar evento IR venda para cliente {ClienteId}",
                    evento.ClienteId);
            }
        }
    }
}
