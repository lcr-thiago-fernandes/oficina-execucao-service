# Arquitetura do Execução Service

Visão geral, diagramas e execução local estão no [README principal](../../README.md). Este diretório
guarda as decisões de arquitetura (ADRs) do serviço.

## Camadas (`src/`)

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `Oficina.Execucao.Dominio` | Agregado `ExecucaoOs`, `StatusExecucao`, transições, `RegraCapacidade` | nada |
| `Oficina.Execucao.Aplicacao` | Handlers (enfileirar, avançar, consultar), portas (`IRepositorioExecucao`, `IPublicadorMensagens`, `ITelemetriaExecucao`), contratos de mensagem | Domínio |
| `Oficina.Execucao.Infraestrutura` | DynamoDB, MassTransit (SQS/SNS/InMemory), consumidor `IniciarExecucao`, New Relic | Aplicação |
| `Oficina.Execucao.Api` | Controllers, JWT, Swagger, middlewares de correlação e exceções, `PathBase /execucao` | todos |

A regra de dependência aponta para dentro: o domínio não conhece AWS nem MassTransit.

## Decisões

- [ADR-001 — DynamoDB single-table com contador por unidade](ADR-001-dynamodb-contador-por-unidade.md)
- [ADR-002 — Idempotência por escrita condicional e republicação, sem outbox](ADR-002-idempotencia-sem-outbox.md)

## Papel na saga

Participante da saga coreografada da OS; o orquestrador é o `oficina-app`. A **entrada na fila é o
passo pivô** (spec §4.2): `ExecucaoEnfileirada` confirma o ponto sem volta; `ExecucaoRecusada` (capacidade
esgotada ou comando inválido) dispara a compensação (estorno e cancelamento do orçamento).
Design completo:
[fase4-design-arquitetural.md](https://github.com/lcr-thiago-fernandes/oficina-app/blob/develop/docs/arquitetura/fase4-design-arquitetural.md).
