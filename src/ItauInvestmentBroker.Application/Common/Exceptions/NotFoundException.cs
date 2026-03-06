namespace ItauInvestmentBroker.Application.Common.Exceptions;

public class NotFoundException(string mensagem, string codigo) : BusinessException(mensagem, codigo);
