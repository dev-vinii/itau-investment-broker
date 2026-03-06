namespace ItauInvestmentBroker.Application.Exceptions;

public class NotFoundException(string mensagem, string codigo) : BusinessException(mensagem, codigo);
