# ADR-002 — Idempotência por escrita condicional e republicação, sem outbox

- **Status:** aceita
- **Contexto:** spec §4.2 e §13 do [design da Fase 4](https://github.com/lcr-thiago-fernandes/oficina-app/blob/develop/docs/arquitetura/fase4-design-arquitetural.md)

## Contexto

O serviço grava no DynamoDB e publica no SNS/SQS. Os dois não compartilham transação: se o
processo cair entre a gravação e a publicação, o estado muda mas a saga nunca recebe o evento.
O padrão clássico seria um **outbox** (tabela + relay).

## Decisão

Sem outbox. A ordem é **gravar primeiro, publicar depois**, e o consumidor de `IniciarExecucao`
é **idempotente**:

1. Chegou `IniciarExecucao` para uma OS que já existe? Não regrava; **republica** o resultado já
   gravado (`ExecucaoEnfileirada` com a posição atual, ou `ExecucaoRecusada` com o motivo salvo).
2. A criação usa escrita condicional (`attribute_not_exists(osId)`); se duas entregas disputarem,
   uma recebe `JaExistente` e cai no caso 1.
3. Se a publicação falhar, a exceção sobe, o MassTransit repete a mensagem e o passo 1 refaz só a
   publicação.

O pivô da saga (spec §4.2) é a entrada na fila: depois que `ExecucaoEnfileirada` é aceito, a
compensação passa a ser por eventos de negócio, não por desfazer a gravação.

### Retry e falha final

Retry exponencial 3x (1s a 30s) no endpoint `{prefixo}-oficina-execucao-comandos`, **sem**
redelivery atrasado; esgotadas as tentativas a mensagem vai para a fila `_error`.
`PublishFaults = false`: `Fault<T>` criaria tópicos fora do padrão `${amb}-oficina-*` do IAM.

**Desvio consciente de spec §4.2/§13:** a spec prevê também redelivery atrasado. Foi omitido para manter o transporte simples: com a idempotência acima, reprocessar a `_error`
manualmente é seguro, e a falha final fica visível na fila `_error` em vez de ser adiada.

## Consequências

- (+) Sem tabela/relay extra; menos peças para operar.
- (+) Reentrega, duplicata do produtor e réplicas concorrentes convergem para o mesmo estado.
- (-) Janela entre gravar e publicar: coberta apenas pela reentrega da mensagem de entrada. Se o
  comando original se perder (não for reentregue), a saga do `oficina-app` precisa de timeout próprio.
- (-) **Pendência (F15) — resolvida:** `IniciarExecucao` duplicado com a OS já fora da fila republica
  `ExecucaoEnfileirada` com `PosicaoFila = 0`; a saga do OS Service ignora esse evento fora de `AguardandoExecucao`
  (ver [contratos](../contratos.md)).
