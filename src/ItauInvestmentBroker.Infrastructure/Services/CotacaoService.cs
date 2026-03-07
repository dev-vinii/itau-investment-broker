using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Application.Common.Models;
using ItauInvestmentBroker.Infrastructure.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Infrastructure.Services;

public class CotacaoService : ICotacaoService
{
    private readonly CotahistParser _parser = new();
    private readonly Dictionary<string, Cotacao> _cotacoes = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _directoryPath;

    public CotacaoService(IConfiguration configuration, ILogger<CotacaoService> logger)
    {
        _directoryPath = configuration[CotahistConstants.DirectoryPathKey];
        if (string.IsNullOrEmpty(_directoryPath) || !Directory.Exists(_directoryPath))
        {
            logger.LogWarning("Diretório de cotações não encontrado: {Path}", _directoryPath);
            return;
        }

        var arquivos = Directory.GetFiles(_directoryPath, CotahistConstants.ArquivoPattern)
            .OrderBy(f => f)
            .ToList();

        if (arquivos.Count == 0)
        {
            logger.LogWarning("Nenhum arquivo COTAHIST encontrado em {Path}", _directoryPath);
            return;
        }

        foreach (var arquivo in arquivos)
        {
            var cotacoes = _parser.ParseArquivo(arquivo);
            foreach (var cotacao in cotacoes.Where(c =>
                         c.CodigoBDI is CotahistConstants.CodigoBdiLotePadrao or CotahistConstants.CodigoBdiFracionario
                         && !string.IsNullOrWhiteSpace(c.Ticker)
                         && c.PrecoFechamento > 0))
            {
                // RN-027: manter a cotacao de fechamento mais recente disponivel no COTAHIST.
                _cotacoes[cotacao.Ticker] = cotacao;
            }
        }

        logger.LogInformation("{Count} cotações carregadas de {Arquivos} arquivo(s)", _cotacoes.Count, arquivos.Count);
    }

    public Cotacao? ObterCotacao(string ticker)
    {
        return _cotacoes.GetValueOrDefault(ticker);
    }

    public IReadOnlyDictionary<string, Cotacao> ObterTodasCotacoes()
    {
        return _cotacoes;
    }
}
