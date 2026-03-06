using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.Services;

public class CustodiaAppService(
    ICustodiaRepository custodiaRepository,
    IDateTimeProvider dateTimeProvider)
{
    public async Task AtualizarCustodia(
        long contaGraficaId,
        string ticker,
        int quantidadeNova,
        decimal precoCompra,
        CancellationToken cancellationToken)
    {
        var custodia = await custodiaRepository.FindByContaGraficaIdAndTicker(
            contaGraficaId, ticker, cancellationToken);

        if (custodia is null)
        {
            custodia = new Custodia
            {
                Ticker = ticker,
                Quantidade = quantidadeNova,
                PrecoMedio = precoCompra,
                ContaGraficaId = contaGraficaId,
                DataUltimaAtualizacao = dateTimeProvider.UtcNow
            };
            custodiaRepository.Add(custodia);
        }
        else
        {
            // RN-042: PM = (Qtd Anterior x PM Anterior + Qtd Nova x Preco Novo) / (Qtd Anterior + Qtd Nova)
            custodia.PrecoMedio =
                (custodia.Quantidade * custodia.PrecoMedio + quantidadeNova * precoCompra)
                / (custodia.Quantidade + quantidadeNova);
            custodia.Quantidade += quantidadeNova;
            custodia.DataUltimaAtualizacao = dateTimeProvider.UtcNow;
        }
    }

    public async Task<VendaInfo?> VenderPosicao(
        long contaGraficaId,
        string ticker,
        ICotacaoService cotacaoService,
        CancellationToken cancellationToken)
    {
        var custodia = await custodiaRepository.FindByContaGraficaIdAndTicker(
            contaGraficaId, ticker, cancellationToken);
        if (custodia is null || custodia.Quantidade == 0)
            return null;

        var cotacao = cotacaoService.ObterCotacao(ticker);
        if (cotacao is null)
            return null;

        var valorVenda = custodia.Quantidade * cotacao.PrecoFechamento;
        var lucro = custodia.Quantidade * (cotacao.PrecoFechamento - custodia.PrecoMedio);
        var resultado = new VendaInfo(ticker, custodia.Quantidade, cotacao.PrecoFechamento, custodia.PrecoMedio, lucro, valorVenda);

        // RN-043: Venda nao altera PM, apenas zera quantidade.
        custodia.Quantidade = 0;
        custodia.DataUltimaAtualizacao = dateTimeProvider.UtcNow;

        return resultado;
    }
}

public record VendaInfo(string Ticker, int Quantidade, decimal PrecoVenda, decimal PrecoMedio, decimal Lucro, decimal ValorVenda);
