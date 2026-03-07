namespace ItauInvestmentBroker.Application.Features.Motor.DTOs;

public record RebalanceamentoCarteiraCommand(
    long CestaAntigaId,
    long CestaNovaId,
    DateTime DataSolicitacao
);
