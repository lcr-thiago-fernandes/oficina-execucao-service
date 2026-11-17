# Copie para local.tfvars (ignorado pelo git) para aplicar da propria maquina.
# Pre-requisito: os parametros SSM listados em docs/contratos.md ja publicados pelo
# oficina-infra-k8s e pelo oficina-lambda-auth.

region  = "us-east-1"
project = "oficina"

github_owner = "lcr-thiago-fernandes"
github_repo  = "oficina-execucao-service"

ecr_keep_last_images = 10
