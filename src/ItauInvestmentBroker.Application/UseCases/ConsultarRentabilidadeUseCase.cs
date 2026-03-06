using ItauInvestmentBroker.Application.DTOs.Rentabilidade;
using ItauInvestmentBroker.Application.Exceptions;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Application.UseCases;

public class ConsultarRentabilidadeUseCase(
    IClienteRepository clienteRepository,
    IContaGraficaRepository contaGraficaRepository,
    ICustodiaRepository custodiaRepository,
    ICotacaoService cotacaoService)
{
    public async Task<RentabilidadeResponse> Executar(long clienteId, CancellationToken cancellationToken = default)
    {
        var cliente = await clienteRepository.FindById(clienteId, cancellationToken)
            ?? throw new NotFoundException(
                $"Cliente com ID {clienteId} não encontrado.",
                ErrorCodes.ClienteNaoEncontrado);

        // RN-010: Cliente pode consultar carteira mesmo apos sair (nao valida Ativo=true aqui).
        var contaGrafica = await contaGraficaRepository.FindByClienteId(clienteId, cancellationToken)
            ?? throw new NotFoundException(
                $"Conta gráfica não encontrada para o cliente {clienteId}.",
                ErrorCodes.ClienteNaoEncontrado);

        var custodias = (await custodiaRepository.FindByContaGraficaId(contaGrafica.Id, cancellationToken)).ToList();

        var ativos = new List<AtivoRentabilidadeResponse>();
        decimal valorInvestidoTotal = 0;
        decimal valorAtualTotal = 0;

        foreach (var custodia in custodias)
        {
            if (custodia.Quantidade == 0)
                continue;

            var cotacao = cotacaoService.ObterCotacao(custodia.Ticker);
            var cotacaoAtual = cotacao?.PrecoFechamento ?? 0;

            var valorAtual = custodia.Quantidade * cotacaoAtual;
            var valorInvestido = custodia.Quantidade * custodia.PrecoMedio;
            var pl = valorAtual - valorInvestido;

            valorInvestidoTotal += valorInvestido;
            valorAtualTotal += valorAtual;

            ativos.Add(new AtivoRentabilidadeResponse(
                Ticker: custodia.Ticker,
                // RN-068: Exibir quantidade por ativo.
                Quantidade: custodia.Quantidade,
                // RN-067: Exibir preco medio por ativo.
                PrecoMedio: Math.Round(custodia.PrecoMedio, 2),
                // RN-069: Exibir cotacao atual por ativo.
                CotacaoAtual: cotacaoAtual,
                ValorAtual: Math.Round(valorAtual, 2),
                // RN-064: Exibir P/L por ativo.
                Pl: Math.Round(pl, 2),
                ComposicaoPercentual: 0
            ));
        }

        // RN-070: Composicao percentual real da carteira por ativo.
        var ativosComPercentual = ativos.Select(a => a with
        {
            ComposicaoPercentual = valorAtualTotal > 0
                ? Math.Round(a.ValorAtual / valorAtualTotal * 100, 2)
                : 0
        }).ToList();

        // RN-065: P/L total da carteira.
        var plTotal = valorAtualTotal - valorInvestidoTotal;
        // RN-066: Rentabilidade percentual da carteira.
        var rentabilidade = valorInvestidoTotal > 0
            ? Math.Round((valorAtualTotal - valorInvestidoTotal) / valorInvestidoTotal * 100, 2)
            : 0;

        return new RentabilidadeResponse(
            ClienteId: cliente.Id,
            Nome: cliente.Nome,
            // RN-063: Saldo total/valor investido e valor atual total da carteira.
            ValorInvestidoTotal: Math.Round(valorInvestidoTotal, 2),
            ValorAtualTotal: Math.Round(valorAtualTotal, 2),
            PlTotal: Math.Round(plTotal, 2),
            RentabilidadePercentual: rentabilidade,
            Ativos: ativosComPercentual
        );
    }
}
