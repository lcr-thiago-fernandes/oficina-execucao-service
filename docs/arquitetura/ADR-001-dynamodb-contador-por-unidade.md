# ADR-001 — DynamoDB single-table com contador de ativas por unidade

- **Status:** aceita
- **Contexto:** spec §2.4 e §6.3 do [design da Fase 4](https://github.com/lcr-thiago-fernandes/oficina-app/blob/develop/docs/arquitetura/fase4-design-arquitetural.md)

## Contexto

A execução limita quantas OS ficam ativas (`NaFila`, `EmDiagnostico`, `EmReparo`) por unidade
(`Execucao__CapacidadeMaximaPorUnidade`, padrão 20). Duas réplicas podem receber `IniciarExecucao`
ao mesmo tempo; o limite não pode ser ultrapassado por corrida. O acesso é sempre por `osId`
(chave) ou por status/ordem de chegada (fila).

## Decisão

Uma tabela DynamoDB (`oficina-execucao-<amb>`, `PAY_PER_REQUEST`, PITR ligado) com dois tipos de item:

| Item | Chave `osId` | Conteúdo |
|---|---|---|
| Execução | `<guid da OS>` | status, unidade, itens, `enfileiradaEm`, versão (lock otimista) |
| Contador | `UNIDADE#<unidade>` | `ativas` (número de execuções ativas da unidade) |

- **Enfileirar** é uma `TransactWriteItems` com duas operações: `Put` da execução
  (`attribute_not_exists(osId)`) e `Update` do contador (`SET ativas = if_not_exists(ativas, 0) + 1`
  com condição `attribute_not_exists(ativas) OR ativas < :max`). Ou as duas valem ou nenhuma:
  a condição do contador é o que impede ultrapassar o limite sob concorrência.
- **Finalizar** decrementa o contador (`ativas > 0`) na mesma transação que muda o status.
- **Capacidade <= 0** (demo de compensação): o handler recusa **sem escrever** no contador;
  grava só a execução `Recusada` (que não conta como ativa).
- `TransactionCanceledException` por conflito transitório é repetida (até 5 tentativas);
  condição falha = capacidade esgotada ou OS já existente (idempotência, ver ADR-002).

### Por que não contar vagas pelo GSI

O GSI `status-enfileiradaEm` é **eventualmente consistente**: contar nele para decidir "há vaga?"
permitiria duas réplicas lerem a mesma contagem e ambas aceitarem. O contador é lido e
atualizado de forma fortemente consistente dentro da transação condicional.

### Posição na fila

`PosicaoFila = count(execuções NaFila da unidade com enfileiradaEm < a minha) + 1`. É só informativa
(vai em `ExecucaoEnfileirada`), então ler o GSI (eventualmente consistente) é aceitável: no pior caso a
posição reportada está defasada por instantes. Por isso ela **não** é usada para nenhuma decisão.

## Consequências

- (+) Limite exato por unidade sem lock distribuído nem serviço extra.
- (+) Uma tabela, um IAM, um backup.
- (-) O contador é um ponto quente por unidade (aceitável: dezenas de OS por unidade).
- (-) Contador e itens podem divergir se alguém editar a tabela à mão; não há rotina de reconciliação (fora do escopo).
