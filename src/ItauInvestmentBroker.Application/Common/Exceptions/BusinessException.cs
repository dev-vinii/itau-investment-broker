namespace ItauInvestmentBroker.Application.Common.Exceptions;

public class BusinessException(string mensagem, string codigo) : Exception(mensagem)
{
    public string Codigo { get; } = codigo;
}
