namespace ItauInvestmentBroker.Application.Exceptions;

public static class ErrorCodes
{
    // Cliente
    public const string ClienteCpfDuplicado = "CLIENTE_CPF_DUPLICADO";
    public const string ClienteNaoEncontrado = "CLIENTE_NAO_ENCONTRADO";
    public const string ClienteJaInativo = "CLIENTE_JA_INATIVO";
    public const string ValorMensalInvalido = "VALOR_MENSAL_INVALIDO";

    // Cesta
    public const string CestaNaoEncontrada = "CESTA_NAO_ENCONTRADA";
    public const string PercentuaisInvalidos = "PERCENTUAIS_INVALIDOS";
    public const string QuantidadeAtivosInvalida = "QUANTIDADE_ATIVOS_INVALIDA";
    public const string TickersDuplicados = "TICKERS_DUPLICADOS";

    // Motor de Compra
    public const string CompraJaExecutada = "COMPRA_JA_EXECUTADA";
    public const string CotacaoNaoEncontrada = "COTACAO_NAO_ENCONTRADA";

    // Infraestrutura
    public const string KafkaIndisponivel = "KAFKA_INDISPONIVEL";
}
