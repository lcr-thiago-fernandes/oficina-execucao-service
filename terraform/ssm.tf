# Contrato publicado por este repositorio (RFC-004), sob /oficina/execucao/.
# Lidos pelo cd.yml: ecr_repository_url e <amb>/irsa_role_arn.

resource "aws_ssm_parameter" "ecr_repository_url" {
  name        = "/${var.project}/execucao/ecr_repository_url"
  description = "URL do repositorio ECR do oficina-execucao (lida pelo cd.yml)."
  type        = "String"
  value       = aws_ecr_repository.execucao.repository_url
}

resource "aws_ssm_parameter" "irsa_role_arn" {
  for_each = local.ambientes

  name        = "/${var.project}/execucao/${each.key}/irsa_role_arn"
  description = "Role IRSA da ServiceAccount oficina-execucao em ${each.value.namespace} (lida pelo cd.yml)."
  type        = "String"
  value       = aws_iam_role.irsa[each.key].arn
}

resource "aws_ssm_parameter" "dynamodb_table" {
  for_each = local.ambientes

  name        = "/${var.project}/execucao/${each.key}/dynamodb_table"
  description = "Tabela DynamoDB do oficina-execucao no ambiente ${each.key}."
  type        = "String"
  value       = aws_dynamodb_table.execucao[each.key].name
}
