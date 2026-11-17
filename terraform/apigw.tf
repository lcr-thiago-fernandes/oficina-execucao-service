# O HTTP API, o stage $default e o VPC Link sao do oficina-infra-k8s; o authorizer e do
# oficina-lambda-auth (ids lidos do SSM). Este repositorio pendura as integracoes com o
# listener PRD do servico (8083) e as rotas sob /execucao. O HTTP_PROXY repassa o path
# inteiro: a API usa PathBase /execucao.

# Integracao da rota PROTEGIDA: mapeia o contexto do authorizer em headers informativos,
# como a integracao do infra-k8s — o servico revalida o JWT por conta propria.
resource "aws_apigatewayv2_integration" "execucao" {
  api_id                 = local.api_id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = aws_lb_listener.execucao["prd"].arn
  connection_type        = "VPC_LINK"
  connection_id          = local.vpc_link_id
  payload_format_version = "1.0"
  timeout_milliseconds   = 29000

  request_parameters = {
    "append:header.X-Perfil"    = "$context.authorizer.perfil"
    "append:header.X-Sub"       = "$context.authorizer.sub"
    "append:header.X-Documento" = "$context.authorizer.documento"
  }
}

# Integracao das rotas PUBLICAS: mesmo listener, SEM o mapeamento de
# $context.authorizer.* (nessas rotas o contexto nao existe e o mapeamento nao resolvido
# poderia virar 500 justamente no /health).
resource "aws_apigatewayv2_integration" "execucao_publica" {
  api_id                 = local.api_id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = aws_lb_listener.execucao["prd"].arn
  connection_type        = "VPC_LINK"
  connection_id          = local.vpc_link_id
  payload_format_version = "1.0"
  timeout_milliseconds   = 29000
}

resource "aws_apigatewayv2_route" "execucao_protegida" {
  api_id             = local.api_id
  route_key          = "ANY /execucao/api/v1/{proxy+}"
  target             = "integrations/${aws_apigatewayv2_integration.execucao.id}"
  authorization_type = "CUSTOM"
  authorizer_id      = local.authorizer_id
}

locals {
  rotas_publicas = {
    health        = "GET /execucao/health"
    swagger       = "GET /execucao/swagger" # {proxy+} nao casa com /swagger sem sufixo (decisao D5 do infra-k8s)
    swagger_proxy = "GET /execucao/swagger/{proxy+}"
  }
}

resource "aws_apigatewayv2_route" "execucao_publica" {
  for_each = local.rotas_publicas

  api_id    = local.api_id
  route_key = each.value
  target    = "integrations/${aws_apigatewayv2_integration.execucao_publica.id}"
}
