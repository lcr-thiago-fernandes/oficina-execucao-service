# Mesmo bucket e tabela de lock criados pelo scripts/bootstrap.sh do oficina-infra-k8s;
# key propria: nenhum repositorio le o state do outro (contrato so por SSM, RFC-004).
# use_lockfile (lock nativo no S3, Terraform >= 1.10) + dynamodb_table (transicao, como
# nos outros repositorios).
terraform {
  backend "s3" {
    bucket         = "oficina-tfstate-fiap-15soat"
    key            = "execucao-service/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "oficina-tfstate-lock"
    use_lockfile   = true
    encrypt        = true
  }
}
