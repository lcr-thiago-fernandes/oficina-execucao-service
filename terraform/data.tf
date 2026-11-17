# Contrato entre repositorios (RFC-004): identificadores no SSM, nunca terraform_remote_state.
# Se um parametro nao existir, o plan falha aqui, com o nome do parametro na mensagem.
# insecure_value: sao identificadores, nao segredos; `value` e sempre sensitive no provider
# e nao pode alimentar for_each (ASGs) nem aparecer legivel no plan.

data "aws_caller_identity" "atual" {}

# ---------- existentes (oficina-infra-k8s / oficina-lambda-auth) ----------
data "aws_ssm_parameter" "vpc_id" {
  name = "/${var.project}/network/vpc_id"
}

data "aws_ssm_parameter" "eks_node_sg_id" {
  name = "/${var.project}/network/eks_node_sg_id"
}

data "aws_ssm_parameter" "eks_cluster_name" {
  name = "/${var.project}/eks/cluster_name"
}

data "aws_ssm_parameter" "apigw_api_id" {
  name = "/${var.project}/apigw/api_id"
}

data "aws_ssm_parameter" "apigw_vpc_link_id" {
  name = "/${var.project}/apigw/vpc_link_id"
}

data "aws_ssm_parameter" "lambda_authorizer_id" {
  name = "/${var.project}/auth/lambda_authorizer_id"
}

# ---------- novos: contrato a publicar pelo oficina-infra-k8s (docs/contratos.md) ----------
data "aws_ssm_parameter" "eks_oidc_provider_arn" {
  name = "/${var.project}/eks/oidc_provider_arn"
}

data "aws_ssm_parameter" "eks_oidc_provider_url" {
  name = "/${var.project}/eks/oidc_provider_url"
}

data "aws_ssm_parameter" "nlb_arn" {
  name = "/${var.project}/nlb/arn"
}

data "aws_ssm_parameter" "nlb_security_group_id" {
  name = "/${var.project}/nlb/security_group_id"
}

data "aws_ssm_parameter" "apigw_vpc_link_security_group_id" {
  name = "/${var.project}/apigw/vpc_link_security_group_id"
}

data "aws_ssm_parameter" "eks_node_asg_names" {
  name = "/${var.project}/eks/node_asg_names" # StringList (separada por virgula)
}

data "aws_vpc" "oficina" {
  id = data.aws_ssm_parameter.vpc_id.insecure_value
}

locals {
  cluster_name      = data.aws_ssm_parameter.eks_cluster_name.insecure_value
  api_id            = data.aws_ssm_parameter.apigw_api_id.insecure_value
  vpc_link_id       = data.aws_ssm_parameter.apigw_vpc_link_id.insecure_value
  authorizer_id     = data.aws_ssm_parameter.lambda_authorizer_id.insecure_value
  oidc_provider_arn = data.aws_ssm_parameter.eks_oidc_provider_arn.insecure_value
  oidc_provider_url = trimprefix(data.aws_ssm_parameter.eks_oidc_provider_url.insecure_value, "https://") # defesa: o contrato ja e sem https://
  nlb_arn           = data.aws_ssm_parameter.nlb_arn.insecure_value
  nlb_sg_id         = data.aws_ssm_parameter.nlb_security_group_id.insecure_value
  vpclink_sg_id     = data.aws_ssm_parameter.apigw_vpc_link_security_group_id.insecure_value
  node_sg_id        = data.aws_ssm_parameter.eks_node_sg_id.insecure_value
  node_asg_names    = toset(compact(split(",", data.aws_ssm_parameter.eks_node_asg_names.insecure_value)))
}
