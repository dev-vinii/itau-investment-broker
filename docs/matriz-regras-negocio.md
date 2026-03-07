# Matriz de Regras de Negocio (RN-001 a RN-070)

Legenda de status:
- Implementada
- Parcial
- Nao Implementada

| RN | Status | Evidencia |
|---|---|---|
| RN-001 | Implementada | `AdesaoRequestValidator.cs:10-27` |
| RN-002 | Implementada | `AdesaoClienteUseCase.cs:21-26`, `ClienteConfiguration.cs:23` |
| RN-003 | Implementada | `AdesaoRequestValidator.cs:24-27`, `ValorMensalRequestValidator.cs:10-13` |
| RN-004 | Implementada | Conta filhote + custodia filhote criadas na adesao em `AdesaoClienteUseCase.cs` |
| RN-005 | Implementada | `Cliente.cs:9`, `AdesaoClienteUseCase.cs:36` |
| RN-006 | Implementada | `Cliente.cs:10`, `AdesaoClienteUseCase.cs:36` |
| RN-007 | Implementada | `SaidaClienteUseCase.cs:20-23` |
| RN-008 | Implementada | `SaidaClienteUseCase.cs:21-23` (nao ha venda) |
| RN-009 | Implementada | Filtro de ativos em `ExecutarCompraUseCase.cs:32-35` |
| RN-010 | Implementada | Consulta sem checar `Ativo=true` em `ConsultarRentabilidadeUseCase.cs:21-25` |
| RN-011 | Implementada | `AtualizarValorMensalUseCase.cs:15-16` |
| RN-012 | Implementada | Valor lido no ciclo atual em `ExecutarCompraUseCase.cs:41-42`; alteracao em `AtualizarValorMensalUseCase.cs:28-31` |
| RN-013 | Implementada | Entidade `HistoricoValorMensal` registra valor anterior/novo em `AtualizarValorMensalUseCase.cs:28-35` |
| RN-014 | Implementada | `CestaRequestValidator.cs:14-17` |
| RN-015 | Implementada | `CestaRequestValidator.cs:19-22` |
| RN-016 | Implementada | `CestaRequestValidator.cs:24-27` |
| RN-017 | Implementada | `CriarAtualizarCestaUseCase.cs:23-26` |
| RN-018 | Implementada | Fluxo em `CriarAtualizarCestaUseCase.cs:20-26` + unique filtered index em `CestaConfiguration.cs` |
| RN-019 | Implementada | `CriarAtualizarCestaUseCase.cs:33-37` |
| RN-020 | Implementada | Agendamento externo no `docker-compose.local.yml` (`scheduler`) + regra 5/15/25 em `docker/scheduler/run-motor.sh` |
| RN-021 | Implementada | Ajuste para proximo dia util (segunda) em `docker/scheduler/run-motor.sh` |
| RN-022 | Implementada | Execucao apenas segunda a sexta via `docker/scheduler/crontab` |
| RN-023 | Implementada | `ExecutarCompraUseCase.cs:21`, `ExecutarCompraUseCase.cs:41-42` |
| RN-024 | Implementada | `ExecutarCompraUseCase.cs:32-35`, `RebalancearPorDesvioUseCase.cs:30-33` |
| RN-025 | Implementada | `ExecutarCompraUseCase.cs:41-42` |
| RN-026 | Implementada | Consolidacao em `ExecutarCompraUseCase.cs:41-42`; ordem unica em `ExecutarCompraUseCase.cs:88-96` |
| RN-027 | Implementada | Cotacao de fechamento mais recente em `CotacaoService.cs:23-25`, `CotacaoService.cs:38-39` |
| RN-028 | Implementada | Truncamento por cast inteiro em `ExecutarCompraUseCase.cs:54-56` |
| RN-029 | Implementada | `ExecutarCompraUseCase.cs:58-61` |
| RN-030 | Implementada | `ExecutarCompraUseCase.cs:63` |
| RN-031 | Implementada | `ExecutarCompraUseCase.cs:71-73` |
| RN-032 | Implementada | `ExecutarCompraUseCase.cs:71-73` |
| RN-033 | Implementada | `ExecutarCompraUseCase.cs:116-117` |
| RN-034 | Implementada | `ExecutarCompraUseCase.cs:140-141` |
| RN-035 | Implementada | `ExecutarCompraUseCase.cs:155-156` |
| RN-036 | Implementada | `ExecutarCompraUseCase.cs:157-158` |
| RN-037 | Implementada | `ExecutarCompraUseCase.cs:65-66` |
| RN-038 | Implementada | `ExecutarCompraUseCase.cs:232-235`, `CustodiaAppService.cs:16-46` |
| RN-039 | Implementada | `ExecutarCompraUseCase.cs:128-138`, `ExecutarCompraUseCase.cs:199-215` |
| RN-040 | Implementada | `ExecutarCompraUseCase.cs:58-63` |
| RN-041 | Implementada | Custodia por conta+ticker em `ExecutarCompraUseCase.cs:256-257` |
| RN-042 | Implementada | `CustodiaAppService.cs:40-44` |
| RN-043 | Implementada | `RebalancearCarteiraUseCase.cs:96-99` |
| RN-044 | Implementada | Recalculo apenas nas compras em `ExecutarCompraUseCase.cs:232-235`, `RebalancearCarteiraUseCase.cs:176-177` |
| RN-045 | Implementada | Disparo por alteracao de cesta em `CriarAtualizarCestaUseCase.cs:33-37` |
| RN-046 | Implementada | Identificacao de saida em `RebalancearCarteiraUseCase.cs:31-32` |
| RN-047 | Implementada | `RebalancearCarteiraUseCase.cs:46-55`, `RebalancearCarteiraUseCase.cs:57-60` |
| RN-048 | Implementada | `RebalancearCarteiraUseCase.cs:101-114`, `RebalancearCarteiraUseCase.cs:119` |
| RN-049 | Implementada | Venda de excesso e compra de deficit para ativos remanescentes em `RebalancearCarteiraUseCase.cs` |
| RN-050 | Implementada | `RebalancearPorDesvioUseCase.cs:26-35` |
| RN-051 | Implementada | Limiar 5 p.p. em `RebalancearPorDesvioUseCase.cs:17`, `RebalancearPorDesvioUseCase.cs:63-80` |
| RN-052 | Implementada | `RebalancearPorDesvioUseCase.cs:98-117`, `RebalancearPorDesvioUseCase.cs:119-159` |
| RN-053 | Implementada | Aliquota 0,005% em `ExecutarCompraUseCase.cs:23`, `ExecutarCompraUseCase.cs:180-182` |
| RN-054 | Implementada | Calculo por operacao em `ExecutarCompraUseCase.cs:180-182` |
| RN-055 | Implementada | Publicacao Kafka em `ExecutarCompraUseCase.cs:224-230`, `RebalancearCarteiraUseCase.cs:173-181` |
| RN-056 | Implementada | Campos no evento `IrDedoDuroEvent` preenchidos em `ExecutarCompraUseCase.cs:184-195` |
| RN-057 | Implementada | Entidade `VendaRebalanceamento` persiste vendas; acumulado mensal via `VendaRebalanceamentoRepository.SomarVendasMes` em `RebalancearCarteiraUseCase.cs` e `RebalancearPorDesvioUseCase.cs` |
| RN-058 | Implementada | `RebalancearCarteiraUseCase.cs:146-150` |
| RN-059 | Implementada | `RebalancearCarteiraUseCase.cs:146-150` |
| RN-060 | Implementada | `RebalancearCarteiraUseCase.cs:91`, `RebalancearCarteiraUseCase.cs:142` |
| RN-061 | Implementada | IR so quando `lucroLiquido > 0` em `RebalancearCarteiraUseCase.cs:146` |
| RN-062 | Implementada | Publicacao `IR_VENDA` em `RebalancearCarteiraUseCase.cs:178-180` |
| RN-063 | Implementada | `ConsultarRentabilidadeUseCase.cs:81-83` |
| RN-064 | Implementada | `ConsultarRentabilidadeUseCase.cs:57-58` |
| RN-065 | Implementada | `ConsultarRentabilidadeUseCase.cs:71-72` |
| RN-066 | Implementada | `ConsultarRentabilidadeUseCase.cs:73-76` |
| RN-067 | Implementada | `ConsultarRentabilidadeUseCase.cs:52-53` |
| RN-068 | Implementada | `ConsultarRentabilidadeUseCase.cs:50-51` |
| RN-069 | Implementada | `ConsultarRentabilidadeUseCase.cs:54-55` |
| RN-070 | Implementada | `ConsultarRentabilidadeUseCase.cs:63-68` |

## Resumo
- Implementadas: 70
- Parciais: 0
- Nao implementadas: 0

Observacao: todas as regras RN-001 a RN-070 estao mapeadas como implementadas.
