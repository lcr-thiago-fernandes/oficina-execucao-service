output "gha_deploy_role_arn" {
  description = "-> secret AWS_DEPLOY_ROLE_ARN deste repositorio (cd.yml)."
  value       = aws_iam_role.gha_deploy.arn
}

output "ecr_repository_url" {
  value = aws_ecr_repository.execucao.repository_url
}

output "irsa_role_arns" {
  description = "Role IRSA por ambiente (tambem em /oficina/execucao/<amb>/irsa_role_arn)."
  value       = { for amb, role in aws_iam_role.irsa : amb => role.arn }
}

output "dynamodb_tables" {
  value = { for amb, tabela in aws_dynamodb_table.execucao : amb => tabela.name }
}
