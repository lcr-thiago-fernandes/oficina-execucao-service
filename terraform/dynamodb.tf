# Uma tabela por ambiente. Esquema IDENTICO a EsquemaTabelaExecucao.CriarRequest
# (src/Oficina.Execucao.Infraestrutura/Persistencia): a aplicacao NAO cria a tabela em
# hml/prd (DynamoDb__CriarTabelaSeAusente = false no ConfigMap).
resource "aws_dynamodb_table" "execucao" {
  for_each = local.ambientes

  name         = "${local.nome_servico}-${each.key}"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "osId"

  attribute {
    name = "osId"
    type = "S"
  }

  attribute {
    name = "status"
    type = "S"
  }

  attribute {
    name = "enfileiradaEm"
    type = "S"
  }

  global_secondary_index {
    name            = "status-enfileiradaEm"
    hash_key        = "status"
    range_key       = "enfileiradaEm"
    projection_type = "ALL"
  }

  point_in_time_recovery {
    enabled = true
  }
}
