# Contratos entre repositórios (SSM e mensageria)

O `terraform/` deste repositório conversa com os outros repositórios **só pelo SSM
Parameter Store** (RFC-004): nenhum `terraform_remote_state`. Se um parâmetro consumido não
existir, o `terraform plan` falha no `data "aws_ssm_parameter"` com o nome dele na mensagem.

Os contratos de mensagens da saga estão em [`contratos/mensagens/`](contratos/mensagens/README.md).

## Consumidos — já existentes

| Parâmetro | Publicado por | Uso aqui |
|---|---|---|
| `/oficina/network/vpc_id` | `oficina-infra-k8s` | VPC dos target groups; CIDR da regra hml (8084) |
| `/oficina/network/eks_node_sg_id` | `oficina-infra-k8s` | Ingress 30084/30085 nos nós a partir do NLB |
| `/oficina/eks/cluster_name` | `oficina-infra-k8s` | Access entry da role de deploy; `eks:DescribeCluster` |
| `/oficina/apigw/api_id` | `oficina-infra-k8s` | Integrações e rotas `/execucao/*` |
| `/oficina/apigw/vpc_link_id` | `oficina-infra-k8s` | `connection_id` das integrações |
| `/oficina/auth/lambda_authorizer_id` | `oficina-lambda-auth` | Authorizer da rota `ANY /execucao/api/v1/{proxy+}` |

## Consumidos — NOVOS (a publicar pelo `oficina-infra-k8s`)

Ainda **não existem**: precisam ser adicionados ao `terraform/ssm.tf` do `oficina-infra-k8s`
antes do primeiro `plan` deste repositório.

| Parâmetro | Tipo | Valor no `oficina-infra-k8s` | Uso aqui |
|---|---|---|---|
| `/oficina/eks/oidc_provider_arn` | String | `module.eks.oidc_provider_arn` | `Federated` da trust IRSA |
| `/oficina/eks/oidc_provider_url` | String | `module.eks.oidc_provider` (**sem** `https://`; o código aplica `trimprefix` por defesa) | Chaves `:sub`/`:aud` da trust IRSA |
| `/oficina/nlb/arn` | String | `aws_lb.interno.arn` | Listeners 8083 (prd) / 8084 (hml) |
| `/oficina/nlb/security_group_id` | String | `aws_security_group.nlb.id` | Regras do SG do NLB |
| `/oficina/apigw/vpc_link_security_group_id` | String | `aws_security_group.vpclink.id` | Egress 8083 do VPC Link para o NLB |
| `/oficina/eks/node_asg_names` | StringList | `join(",", module.eks.eks_managed_node_groups["default"].node_group_autoscaling_group_names)` | `aws_autoscaling_attachment` (ASG × ambiente) |

Outros pré-requisitos no `oficina-infra-k8s`:

- `scripts/bootstrap.sh`: incluir `oficina-execucao-service` em `REPOS_INFRA`, para que a trust
  da role `oficina-gha-infra` (secret `AWS_TERRAFORM_ROLE_ARN` do `infra.yml`) aceite
  `main`/`develop` deste repositório.
- Segredos `oficina/jwt_secret` e `oficina/newrelic_license_key` (já existentes) — lidos pelo
  `cd.yml` com a role `oficina-gha-deploy-execucao`.

## Publicados por este repositório (`/oficina/execucao/`)

| Parâmetro | Valor | Lido por |
|---|---|---|
| `/oficina/execucao/ecr_repository_url` | URL do ECR `oficina-execucao` | `cd.yml` |
| `/oficina/execucao/prd/irsa_role_arn` | Role `oficina-execucao-pod-prd` | `cd.yml` (anotação da ServiceAccount) |
| `/oficina/execucao/hml/irsa_role_arn` | Role `oficina-execucao-pod-hml` | `cd.yml` (anotação da ServiceAccount) |
| `/oficina/execucao/prd/dynamodb_table` | `oficina-execucao-prd` | informativo |
| `/oficina/execucao/hml/dynamodb_table` | `oficina-execucao-hml` | informativo |

Outputs do Terraform: `gha_deploy_role_arn` (→ secret `AWS_DEPLOY_ROLE_ARN` deste
repositório) e `ecr_repository_url`.

## Portas e rotas

| Ambiente | Namespace | NodePort | Listener do NLB | Exposição |
|---|---|---|---|---|
| prd | `oficina-prd` | 30084 | 8083 | API Gateway (VPC Link) |
| hml | `oficina-hml` | 30085 | 8084 | só de dentro da VPC |

Rotas no HTTP API: `ANY /execucao/api/v1/{proxy+}` (authorizer `CUSTOM`) e, sem authorizer,
`GET /execucao/health`, `GET /execucao/swagger`, `GET /execucao/swagger/{proxy+}`.

## Mensageria fixada por este repositório

| Recurso | Nome | Observação |
|---|---|---|
| Fila de comandos | `{prefixo}-oficina-execucao-comandos` | Recebe `IniciarExecucao` por send direto; `_error` para falha final |
| Fila do bus | `{prefixo}-oficina-execucao-bus-{instância}` | Temporária (auto-delete), uma por réplica (nome do pod); casa com `${amb}-oficina-*` do IAM (spec §5) |
| Tópicos | `{prefixo}-oficina-saga-{Nome}` | `ExecucaoEnfileirada`, `ExecucaoRecusada`, `StatusExecucaoAlterado`, `ExecucaoFinalizada` |

Contratos (JSON em [`contratos/mensagens/`](contratos/mensagens/README.md)): consome `IniciarExecucao`; publica
`ExecucaoEnfileirada`, `ExecucaoRecusada`, `StatusExecucaoAlterado`, `ExecucaoFinalizada`. URN `oficina:saga:<Nome>`,
serialização `JsonSerializerDefaults.Web` com enum como string. `{prefixo}` ∈ `local | hml | prd`.

Roles: `oficina-execucao-pod-{prd,hml}` (IRSA do pod: DynamoDB da tabela, SQS/SNS `${amb}-oficina-*`) e
`oficina-gha-deploy-execucao` (deploy do CD, restrita aos namespaces `oficina-prd`/`oficina-hml`).

## Pendências entre repositórios

1. **`oficina-infra-k8s`:** publicar os 6 parâmetros SSM novos listados acima.
2. **`oficina-infra-k8s/scripts/bootstrap.sh`:** incluir `oficina-execucao-service` em `REPOS_INFRA` (trust da role `oficina-gha-infra`).
3. **Ordem de subida:** `oficina-infra-k8s` → `oficina-lambda-auth` → `terraform/` deste repositório
   (`infra.yml` com `workflow_dispatch`, `acao=apply`) → secret `AWS_DEPLOY_ROLE_ARN` (output `gha_deploy_role_arn`) → CD.
4. **OS Service (F15):** `IniciarExecucao` duplicado com a OS já fora da fila republica `ExecucaoEnfileirada`
   com `PosicaoFila = 0`. A saga do `oficina-app` deve **ignorar** esse evento fora de `AguardandoExecucao`.
   Ver [ADR-002](arquitetura/ADR-002-idempotencia-sem-outbox.md).
