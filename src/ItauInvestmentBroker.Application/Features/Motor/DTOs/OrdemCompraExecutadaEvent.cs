namespace ItauInvestmentBroker.Application.Features.Motor.DTOs;

public record OrdemCompraExecutadaEvent(
    string Tipo,
    long OrdemCompraId,
    long CestaId,
    long ContaGraficaMasterId,
    int TotalClientes,
    decimal ValorTotal,
    DateTime DataExecucao
);
