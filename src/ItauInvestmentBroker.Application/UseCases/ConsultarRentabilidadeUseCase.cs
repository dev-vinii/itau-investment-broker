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
                Quantidade: custodia.Quantidade,
                PrecoMedio: Math.Round(custodia.PrecoMedio, 2),
                CotacaoAtual: cotacaoAtual,
                ValorAtual: Math.Round(valorAtual, 2),
                Pl: Math.Round(pl, 2),
                ComposicaoPercentual: 0
            ));
        }

        // Calcular composição percentual
        var ativosComPercentual = ativos.Select(a => a with
        {
            ComposicaoPercentual = valorAtualTotal > 0
                ? Math.Round(a.ValorAtual / valorAtualTotal * 100, 2)
                : 0
        }).ToList();

        var plTotal = valorAtualTotal - valorInvestidoTotal;
        var rentabilidade = valorInvestidoTotal > 0
            ? Math.Round((valorAtualTotal - valorInvestidoTotal) / valorInvestidoTotal * 100, 2)
            : 0;

        return new RentabilidadeResponse(
            ClienteId: cliente.Id,
            Nome: cliente.Nome,
            ValorInvestidoTotal: Math.Round(valorInvestidoTotal, 2),
            ValorAtualTotal: Math.Round(valorAtualTotal, 2),
            PlTotal: Math.Round(plTotal, 2),
            RentabilidadePercentual: rentabilidade,
            Ativos: ativosComPercentual
        );
    }
}
