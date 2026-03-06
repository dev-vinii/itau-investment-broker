using ItauInvestmentBroker.Application.Configuration;
using ItauInvestmentBroker.Application.DTOs.Motor;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace ItauInvestmentBroker.Application.Services;

public class IrCalculationService(
    IVendaRebalanceamentoRepository vendaRebalanceamentoRepository,
    IDateTimeProvider dateTimeProvider,
    IOptions<MotorSettings> motorSettings)
{
    private readonly MotorSettings _settings = motorSettings.Value;

    public IrDedoDuroEvent CalcularIrDedoDuro(
        long clienteId,
        string cpf,
        string ticker,
        string tipoOperacao,
        int quantidade,
        decimal precoUnitario,
        DateTime dataOperacao)
    {
        var valorOperacao = quantidade * precoUnitario;
        var valorIr = Math.Round(valorOperacao * _settings.AliquotaIrDedoDuro, 2);

        return new IrDedoDuroEvent(
            Tipo: "IR_DEDO_DURO",
            ClienteId: clienteId,
            Cpf: cpf,
            Ticker: ticker,
            TipoOperacao: tipoOperacao,
            Quantidade: quantidade,
            PrecoUnitario: precoUnitario,
            ValorOperacao: valorOperacao,
            Aliquota: _settings.AliquotaIrDedoDuro,
            ValorIR: valorIr,
            DataOperacao: dataOperacao
        );
    }

    public async Task<IrVendaEvent?> CalcularIrVenda(
        Cliente cliente,
        List<VendaInfo> vendasCliente,
        CancellationToken cancellationToken)
    {
        if (vendasCliente.Count == 0)
            return null;

        var agora = dateTimeProvider.UtcNow;
        var vendasAnterioresMes = await vendaRebalanceamentoRepository
            .SomarVendasMes(cliente.Id, agora.Year, agora.Month, cancellationToken);
        var lucroAnteriorMes = await vendaRebalanceamentoRepository
            .SomarLucroMes(cliente.Id, agora.Year, agora.Month, cancellationToken);

        var totalVendasExecucao = vendasCliente.Sum(v => v.ValorVenda);
        var lucroExecucao = vendasCliente.Sum(v => v.Lucro);

        var totalVendasMes = vendasAnterioresMes + totalVendasExecucao;
        var lucroLiquidoMes = lucroAnteriorMes + lucroExecucao;

        decimal valorIr = 0;
        decimal aliquota = 0;

        // RN-058/RN-059: Se vendas > limite e lucro > 0, aplica aliquota.
        if (totalVendasMes > _settings.LimiteIsencaoVendas && lucroLiquidoMes > 0)
        {
            aliquota = _settings.AliquotaIrVenda;
            valorIr = Math.Round(lucroLiquidoMes * _settings.AliquotaIrVenda, 2);
        }

        return new IrVendaEvent(
            Tipo: "IR_VENDA",
            ClienteId: cliente.Id,
            Cpf: cliente.Cpf,
            MesReferencia: agora.ToString("yyyy-MM"),
            TotalVendasMes: Math.Round(totalVendasMes, 2),
            LucroLiquido: Math.Round(lucroLiquidoMes, 2),
            Aliquota: aliquota,
            ValorIR: valorIr,
            Detalhes: vendasCliente.Select(v => new IrVendaDetalheEvent(
                v.Ticker, v.Quantidade, v.PrecoVenda, Math.Round(v.PrecoMedio, 2), Math.Round(v.Lucro, 2)
            )).ToList(),
            DataCalculo: agora
        );
    }

    public void PersistirVendas(
        long clienteId,
        List<VendaInfo> vendas)
    {
        foreach (var venda in vendas)
        {
            vendaRebalanceamentoRepository.Add(new VendaRebalanceamento
            {
                ClienteId = clienteId,
                Ticker = venda.Ticker,
                Quantidade = venda.Quantidade,
                PrecoVenda = venda.PrecoVenda,
                PrecoMedio = venda.PrecoMedio,
                ValorVenda = venda.ValorVenda,
                Lucro = venda.Lucro
            });
        }
    }
}
