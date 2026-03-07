namespace ItauInvestmentBroker.Application.Features.Motor.DTOs;

public record MotorExecucaoFalhouEvent(
    string Tipo,
    string Operacao,
    string Erro,
    string? CodigoErro,
    DateTime DataOcorrencia
);
