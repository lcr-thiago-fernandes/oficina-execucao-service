# Manifestos Kubernetes — Serviço de Execução

Orquestração do `oficina-execucao` no EKS (Fase 4), no mesmo cluster e namespaces do
`oficina-app`: `oficina-hml` (branch `develop`) e `oficina-prd` (branch `main`). Os
namespaces **não** são criados aqui: vêm do CD do `oficina-app` (a access entry da role
de deploy é restrita a eles).

| Arquivo | Recurso | O que cria |
|---|---|---|
| `serviceaccount.yaml` | ServiceAccount | `oficina-execucao`, com a role IRSA (`${IRSA_ROLE_ARN}`) para SQS e DynamoDB. |
| `configmap.yaml` | ConfigMap | `oficina-execucao-config`: mensageria SQS, tabela DynamoDB, capacidade por unidade. |
| `secret.yaml` | Secret | `oficina-execucao-secret`: **TEMPLATE** (não aplicado); o CD cria o real a partir do Secrets Manager. |
| `deployment.yaml` | Deployment | 2 réplicas, probes em `/health:8080`, rootfs read-only, New Relic ligado. O pod recebe `HOSTNAME` do Kubernetes (nome da fila SQS por instância). |
| `service.yaml` | Service | `NodePort` 80 → 8080; `30084` (prd) / `30085` (hml). |
| `hpa.yaml` | HPA | 2–10 réplicas, CPU 60%. |

Variáveis substituídas por `envsubst` no CD: `NAMESPACE`, `AMBIENTE` (`hml`|`prd`),
`NODE_PORT`, `ECR_REPOSITORY`, `IMAGE_TAG`, `IRSA_ROLE_ARN`.

O `cd.yml` pula o deploy (aviso, sem falhar) enquanto o secret `AWS_DEPLOY_ROLE_ARN` não
existir — a role só nasce após o primeiro apply manual do `terraform/`.

Demo da saga de compensação (esgota a capacidade):
`kubectl -n oficina-prd set env deployment/oficina-execucao Execucao__CapacidadeMaximaPorUnidade=0`
