using System.Text.Json;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Features.Motor.DTOs;
using ItauInvestmentBroker.Application.Features.Motor.UseCases;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Application.Features.Motor.Handlers;

public class RebalanceamentoCarteiraHandler(
    ICestaRepository cestaRepository,
    RebalancearCarteiraUseCase rebalancearCarteira,
    ILogger<RebalanceamentoCarteiraHandler> logger) : IKafkaMessageHandler
{
    public string Topic => KafkaTopicNames.RebalanceamentoCarteira;

    public async Task HandleAsync(string key, string message, CancellationToken cancellationToken)
    {
        var command = JsonSerializer.Deserialize<RebalanceamentoCarteiraCommand>(message);
        if (command is null)
        {
            logger.LogWarning("Comando de rebalanceamento de carteira invalido: {Message}", message);
            return;
        }

        logger.LogInformation(
            "Iniciando rebalanceamento de carteira. CestaAntiga={CestaAntigaId}, CestaNova={CestaNovaId}",
            command.CestaAntigaId, command.CestaNovaId);

        var cestaAntiga = await cestaRepository.FindByIdWithItens(command.CestaAntigaId, cancellationToken);
        var cestaNova = await cestaRepository.FindByIdWithItens(command.CestaNovaId, cancellationToken);

        if (cestaAntiga is null || cestaNova is null)
        {
            logger.LogError(
                "Cestas nao encontradas para rebalanceamento. CestaAntiga={CestaAntigaId}, CestaNova={CestaNovaId}",
                command.CestaAntigaId, command.CestaNovaId);
            return;
        }

        await rebalancearCarteira.Executar(cestaAntiga, cestaNova, cancellationToken);

        logger.LogInformation("Rebalanceamento de carteira concluido com sucesso");
    }
}
