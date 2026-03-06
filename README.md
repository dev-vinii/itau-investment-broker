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
- API (`:8080`)
- Scheduler

### Subir ambiente

```bash
docker compose -f docker-compose.local.yml up --build
```

Swagger (Development):
- `http://localhost:8080/swagger`

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
