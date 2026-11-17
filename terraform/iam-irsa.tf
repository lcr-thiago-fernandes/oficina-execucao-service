# IRSA: uma role por ambiente, assumida SO pela ServiceAccount oficina-execucao do
# namespace do ambiente (o OIDC provider do EKS e do oficina-infra-k8s, lido do SSM).
# Escopo por prefixo de ambiente: hml nunca toca fila/topico/tabela de prd e vice-versa.

data "aws_iam_policy_document" "irsa_trust" {
  for_each = local.ambientes

  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.oidc_provider_url}:sub"
      values   = ["system:serviceaccount:${each.value.namespace}:${local.service_account}"]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.oidc_provider_url}:aud"
      values   = ["sts.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "irsa" {
  for_each = local.ambientes

  name               = "${local.nome_servico}-pod-${each.key}"
  description        = "IRSA do oficina-execucao em ${each.value.namespace}: DynamoDB, SQS e SNS do ambiente ${each.key}."
  assume_role_policy = data.aws_iam_policy_document.irsa_trust[each.key].json
}

data "aws_iam_policy_document" "irsa" {
  for_each = local.ambientes

  # TransactWriteItems (vaga da unidade + execucao) e autorizado pelas acoes de cada item
  # (PutItem/UpdateItem/ConditionCheckItem); nao existe acao IAM propria.
  statement {
    sid    = "DynamoDbTabelaDoAmbiente"
    effect = "Allow"
    actions = [
      "dynamodb:GetItem",
      "dynamodb:PutItem",
      "dynamodb:UpdateItem",
      "dynamodb:ConditionCheckItem",
      "dynamodb:Query",
      "dynamodb:DescribeTable",
    ]
    resources = [
      aws_dynamodb_table.execucao[each.key].arn,
      "${aws_dynamodb_table.execucao[each.key].arn}/index/*",
    ]
  }

  # Filas do ambiente: comandos, _error/_skipped do MassTransit e a fila do bus por
  # replica (${amb}-oficina-execucao-bus-<pod>, auto-delete: DeleteQueue no shutdown).
  # SetQueueAttributes tambem grava a policy que permite ao SNS entregar na fila assinante.
  statement {
    sid    = "SqsFilasDoAmbiente"
    effect = "Allow"
    actions = [
      "sqs:CreateQueue",
      "sqs:DeleteQueue",
      "sqs:GetQueueUrl",
      "sqs:GetQueueAttributes",
      "sqs:SetQueueAttributes",
      "sqs:SendMessage",
      "sqs:ReceiveMessage",
      "sqs:DeleteMessage",
      "sqs:ChangeMessageVisibility",
      "sqs:PurgeQueue",
      "sqs:TagQueue",
      "sqs:ListQueueTags",
    ]
    resources = ["arn:aws:sqs:${var.region}:${local.conta}:${each.key}-${var.project}-*"]
  }

  # Topicos do ambiente: ${amb}-oficina-saga-<Nome> (SetEntityName por tipo).
  statement {
    sid    = "SnsTopicosDoAmbiente"
    effect = "Allow"
    actions = [
      "sns:CreateTopic",
      "sns:Publish",
      "sns:Subscribe",
      "sns:Unsubscribe",
      "sns:GetTopicAttributes",
      "sns:SetTopicAttributes",
      "sns:ListSubscriptionsByTopic",
      "sns:TagResource",
    ]
    resources = ["arn:aws:sns:${var.region}:${local.conta}:${each.key}-${var.project}-*"]
  }

  # Listagens nao aceitam escopo por recurso.
  statement {
    sid       = "Listagens"
    effect    = "Allow"
    actions   = ["sns:ListTopics", "sqs:ListQueues"]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "irsa" {
  for_each = local.ambientes

  name   = "${local.nome_servico}-pod-${each.key}-policy"
  role   = aws_iam_role.irsa[each.key].id
  policy = data.aws_iam_policy_document.irsa[each.key].json
}
