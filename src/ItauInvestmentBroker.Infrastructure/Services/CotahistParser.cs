using System.Globalization;
using System.Text;
using ItauInvestmentBroker.Application.Common.Models;

namespace ItauInvestmentBroker.Infrastructure.Services;

public class CotahistParser
{
    private const int MinLineLength = 245;
    private const string RecordTypeDetail = "01";
    private const int MarketTypeSpot = 10;
    private const int MarketTypeFractional = 20;
    private const decimal PrecoScaleDivisor = 100m;

    static CotahistParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public IEnumerable<Cotacao> ParseArquivo(string caminhoArquivo)
    {
        var encoding = Encoding.GetEncoding("ISO-8859-1");
        var cotacoes = new List<Cotacao>();

        foreach (var linha in File.ReadLines(caminhoArquivo, encoding))
        {
            if (linha.Length < MinLineLength)
                continue;

            var tipoRegistro = linha.Substring(0, 2);
            if (tipoRegistro != RecordTypeDetail)
                continue;

            if (!int.TryParse(linha.AsSpan(24, 3), out var tipoMercado))
                continue;

            if (tipoMercado is not MarketTypeSpot and not MarketTypeFractional)
                continue;

            try
            {
                var cotacao = new Cotacao
                {
                    DataPregao = DateTime.ParseExact(
                        linha.Substring(2, 8), "yyyyMMdd",
                        CultureInfo.InvariantCulture),
                    CodigoBDI = linha.Substring(10, 2).Trim(),
                    Ticker = linha.Substring(12, 12).Trim(),
                    TipoMercado = tipoMercado,
                    NomeEmpresa = linha.Substring(27, 12).Trim(),
                    PrecoAbertura = ParsePreco(linha.Substring(56, 13)),
                    PrecoMaximo = ParsePreco(linha.Substring(69, 13)),
                    PrecoMinimo = ParsePreco(linha.Substring(82, 13)),
                    PrecoMedio = ParsePreco(linha.Substring(95, 13)),
                    PrecoFechamento = ParsePreco(linha.Substring(108, 13)),
                    QuantidadeNegociada = ParseLong(linha.Substring(152, 18)),
                    VolumeNegociado = ParsePreco(linha.Substring(170, 18))
                };

                cotacoes.Add(cotacao);
            }
            catch (FormatException)
            {
                // Linha com formato invalido — ignorar silenciosamente.
            }
        }

        return cotacoes;
    }

    private static decimal ParsePreco(string valorBruto)
    {
        if (long.TryParse(valorBruto.Trim(), out var valor))
            return valor / PrecoScaleDivisor;
        return 0m;
    }

    private static long ParseLong(string valor)
    {
        return long.TryParse(valor.Trim(), out var result) ? result : 0;
    }
}
