# Itau Investment Broker

API para simular uma corretora de investimentos com:
- adesão/saída de clientes,
- administração de cesta de ativos,
- execução periódica de compras,
- rebalanceamento de carteira,
- cálculo de rentabilidade,
- publicação de eventos fiscais no Kafka.

## Arquitetura

O projeto segue separação por camadas com organização por contexto de domínio e por feature:

- `src/ItauInvestmentBroker.API`
  - Controllers HTTP organizados em `Features/*/Controllers`
  - Middleware global de exceções
  - composição da aplicação (`Program.cs`)
- `src/ItauInvestmentBroker.Application`
  - `Common/` com contratos e componentes transversais (`Interfaces`, `Exceptions`, `Configuration`, `Models`)
  - `Features/Clientes`, `Features/Cestas`, `Features/Motor`, `Features/Rentabilidade`
  - por feature: `DTOs`, `UseCases`, `Validators`, `Mappings`
- `src/ItauInvestmentBroker.Domain`
  - contexts de domínio:
    - `Clientes` (entidades, enums e repositories)
    - `Cestas` (entidades e repositories)
    - `Motor` (entidades, enums e repositories)
  - `Common` (abstrações compartilhadas: `BaseEntity`, `IUnitOfWork`, `IBaseRepository`)
- `src/ItauInvestmentBroker.Infrastructure`
  - EF Core + MySQL (DbContext, migrations, repositories)
  - integração Kafka (producer e consumer)
  - leitura de cotações B3 via COTAHIST

### Direção arquitetural

A organização `DDD + feature-based` foi adotada para:
- reduzir acoplamento entre contextos de negócio;
- facilitar manutenção e evolução incremental do monólito modular;
- preparar uma futura separação em microserviços por contexto de domínio (`Clientes`, `Cestas`, `Motor`, `Rentabilidade`), caso haja necessidade de escalar/deployar de forma independente.

Importante: no estado atual, o sistema continua sendo um monólito modular.

### Diagrama de arquitetura

O diagrama detalhado (draw.io) está em `docs/arquitetura.drawio`.

### Fluxo principal do motor

1. Scheduler dispara `POST /api/motor/executar-compra` nos ciclos 5/15/25 (ajustando para dia útil).
2. Use case consolida aporte dos clientes ativos (`valorMensal / 3`).
3. Compra é executada na conta master com separação lote/fracionário.
4. Ativos são distribuídos para contas filhote proporcionalmente.
5. Custódia e preço médio são atualizados.
6. Eventos fiscais (ex.: `IR_DEDO_DURO`, `IR_VENDA`) são publicados no Kafka.

## Regras do projeto (negócio)

A matriz oficial está em `docs/matriz-regras-negocio.md`.

Resumo atual:
- RN-001 a RN-070 mapeadas
- 70 implementadas
- 0 parciais
- 0 não implementadas

Regras-chave implementadas:
- Cesta deve totalizar 100% e possuir percentuais válidos por ativo.
- Compras usam somente clientes ativos.
- Execução periódica ocorre em 3 ciclos mensais (5, 15, 25).
- Quantidades são truncadas para baixo na alocação.
- Diferenciação de mercado lote e fracionário (`ticker + "F"` para fracionário).
- Rebalanceamento por alteração de cesta e por desvio (limiar de 5 p.p.).
- Cálculos de IR com publicação em Kafka após persistência da operação.

## Endpoints

Base path: `/api`

### Clientes
- `POST /api/clientes/adesao`
- `POST /api/clientes/{id}/saida`
- `PUT /api/clientes/{id}/valor-mensal`
- `GET /api/clientes/{id}/rentabilidade`

### Administração de cesta
- `POST /api/admin/cesta`
- `GET /api/admin/cesta/atual`
- `GET /api/admin/cesta/historico`

### Motor
- `POST /api/motor/executar-compra`
- `POST /api/motor/rebalancear-desvio`

## Infra local

`docker-compose.local.yml` sobe:
- MySQL 8
- Zookeeper
- Kafka
- Kafka Control Center (`:9021`)
- API (`:8080`)
- Scheduler

### Por que o scheduler é externo à API

O disparo do motor de compra foi colocado em um serviço de scheduler (infra), e não dentro da API, para:
- evitar jobs em background concorrendo por CPU/memória com as requisições HTTP;
- manter a API focada em servir tráfego e regras de aplicação síncronas;
- tratar agendamento/retentativa/idempotência como responsabilidade operacional de infraestrutura;
- melhorar previsibilidade de performance e facilitar escala independente (API vs execução periódica).

Em resumo: a API expõe o endpoint do motor e a infraestrutura decide quando executar, com menor impacto de carga e melhor eficiência operacional.

Detalhes operacionais atuais do scheduler:
- logs estruturados em stdout com timestamp, nível, tentativa, HTTP status e duração;
- política de retry com backoff linear para falhas temporárias de rede/API;
- timeout por requisição para evitar execução travada.
- lock de execução para impedir concorrência entre duas instâncias do script;
- marcador `in-progress` por ciclo para bloquear nova execução automática do mesmo ciclo em caso de falha inesperada.
- limites por script para evitar execução inesperada: máximo `1` por dia e `3` por mês (configurável por variável de ambiente).

### Garantia de execução do job (produção)

Em sistemas distribuídos não existe garantia absoluta de execução única, então a estratégia recomendada é combinar mecanismos de confiabilidade:
- execução `at-least-once` com retry (já implementado no scheduler);
- proteção de `at-most-once` no script (lock + `in-progress`) para evitar disparo duplicado automático;
- idempotência por ciclo no motor de compra (evita processamento duplicado mesmo em chamadas manuais);
- estado durável de execução por ciclo (DB/Redis), evitando depender apenas de arquivo local;
- monitoramento e alerta para ciclos esperados não executados (5/15/25);
- rotina de reconciliação para reprocessar automaticamente ciclos pendentes.

Objetivo prático: sair de “torcer para rodar” para “detectar falha rápido e autocorrigir”.

Roadmap (ainda não implementado):
- usar a própria API com flag `schedulerRun=true` + `cycleKey` enviada pelo script;
- validar e registrar no banco apenas execuções do scheduler (limite diário/mensal e unicidade por ciclo);
- manter execuções manuais (Swagger/portal) fora dessa política automática.

Observação: esses bloqueios são aplicados ao fluxo automático via scheduler. Execuções manuais por operador/admin podem seguir política própria da aplicação.

### Evolução para ambiente real (AWS)

Em um cenário real de produção, a estratégia é mover esse scheduler para serviços gerenciados da AWS, por exemplo:
- Amazon EventBridge (agenda/cadência dos disparos);
- AWS Lambda ou ECS Fargate (worker que chama o endpoint do motor).

Assim, o agendamento deixa de depender de container local/cron, ganha observabilidade e resiliência nativas da nuvem e mantém o mesmo princípio arquitetural: execução periódica como responsabilidade de infraestrutura, não da API.

Arquitetura alvo sugerida na AWS para elevar a garantia de execução:
- Amazon EventBridge para agenda confiável dos ciclos (5/15/25);
- Amazon SQS como fila intermediária para desacoplar disparo e processamento;
- worker em AWS Lambda ou ECS Fargate para consumir fila e acionar o motor;
- idempotência por `cycle_key` no motor (com chave única em banco);
- CloudWatch Logs + Metrics + Alarms para detectar falhas e ciclos não executados;
- Dead Letter Queue (DLQ) para mensagens com falha não recuperável.

Resultado esperado: maior confiabilidade operacional, rastreabilidade ponta a ponta e capacidade de recuperação automática sem acoplar agendamento ao runtime da API.

### Subir ambiente

Pré-requisito: Docker + Docker Compose.

```bash
docker compose -f docker-compose.local.yml up --build
```

Esse comando já sobe toda a stack (API, MySQL, Kafka, Zookeeper e scheduler).

Swagger (Development):
- `http://localhost:8080/swagger`

Kafka Control Center:
- `http://localhost:9021`

## Configuração

Arquivo: `src/ItauInvestmentBroker.API/appsettings.json`

Chaves principais:
- `ConnectionStrings:DefaultConnection`
- `Kafka:BootstrapServers`
- `Kafka:GroupId`
- `Cotahist:DirectoryPath`

Observação: o diretório `cotacoes/` deve conter arquivos `COTAHIST_D*.TXT` para o motor usar preços de fechamento.

## Banco e seed

Na inicialização:
- migrations EF Core são aplicadas automaticamente;
- uma conta master técnica é criada caso não exista.

## Testes

Projeto de testes: `tests/ItauInvestmentBroker.Tests`

```bash
dotnet test
```

Cobertura configurada com Coverlet (formato Cobertura).

## Stack

- .NET 10 (`net10.0`)
- ASP.NET Core Web API
- EF Core + MySQL
- FluentValidation
- Mapster
- Kafka (Confluent.Kafka)
- xUnit + FluentAssertions + NSubstitute

## Convencao de commits

Padrao adotado:

`:emoji: tipo: descricao curta no imperativo`

Exemplos:
- `:sparkles: feat: adiciona endpoint de rebalanceamento`
- `:bug: fix: corrige validacao de cpf duplicado`
- `:recycle: refactor: separa use case por responsabilidade`
- `:memo: docs: atualiza matriz de regras de negocio`
- `:wrench: chore: ajusta configuracao do scheduler`

Tipos mais usados:
- `feat`, `fix`, `refactor`, `style`, `test`, `docs`, `chore`, `perf`, `ci`, `build`

Regras:
- usar mensagem em portugues;
- manter descricao curta e direta;
- preferir commits atomicos;
- garantir build e testes aplicaveis antes de commitar.
