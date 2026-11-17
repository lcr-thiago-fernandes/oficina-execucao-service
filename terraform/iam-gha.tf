# Role PROPRIA do CD deste servico (nao reusa oficina-gha-deploy do oficina-app):
# mesmo desenho, escopada ao ECR oficina-execucao, ao SSM /oficina/execucao/* e aos
# namespaces oficina-prd/oficina-hml no cluster.

# OIDC provider do GitHub: criado pelo scripts/bootstrap.sh do oficina-infra-k8s; so lido.
data "aws_iam_openid_connect_provider" "github" {
  url = "https://token.actions.githubusercontent.com"
}

locals {
  github_oidc_host = "token.actions.githubusercontent.com"
  # Trust escopada por BRANCH: so main (prd) e develop (hml). PR nao assume a role.
  subs_deploy = [
    "repo:${var.github_owner}/${var.github_repo}:ref:refs/heads/main",
    "repo:${var.github_owner}/${var.github_repo}:ref:refs/heads/develop",
  ]
}

data "aws_iam_policy_document" "trust_deploy" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [data.aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.github_oidc_host}:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.github_oidc_host}:sub"
      values   = local.subs_deploy
    }
  }
}

resource "aws_iam_role" "gha_deploy" {
  name               = local.nome_role_deploy
  description        = "CD do oficina-execucao-service via OIDC: push no ECR oficina-execucao, describe do EKS, SSM e segredos de runtime."
  assume_role_policy = data.aws_iam_policy_document.trust_deploy.json
}

data "aws_iam_policy_document" "deploy" {
  statement {
    sid       = "EcrAuth"
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"] # a acao exige "*".
  }

  statement {
    sid    = "EcrPushPull"
    effect = "Allow"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:GetDownloadUrlForLayer",
      "ecr:BatchGetImage",
      "ecr:PutImage",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
    ]
    resources = [aws_ecr_repository.execucao.arn]
  }

  # O acesso ao cluster em si vem da access entry abaixo; aqui so o describe do kubeconfig.
  statement {
    sid       = "EksDescribe"
    effect    = "Allow"
    actions   = ["eks:DescribeCluster"]
    resources = ["arn:aws:eks:${var.region}:${local.conta}:cluster/${local.cluster_name}"]
  }

  # Parametros lidos pelo cd.yml: nome do cluster e /oficina/execucao/* (ECR e IRSA).
  statement {
    sid     = "SsmLeitura"
    effect  = "Allow"
    actions = ["ssm:GetParameter", "ssm:GetParameters"]
    resources = [
      "arn:aws:ssm:${var.region}:${local.conta}:parameter/${var.project}/eks/cluster_name",
      "arn:aws:ssm:${var.region}:${local.conta}:parameter/${var.project}/execucao/*",
    ]
  }

  # Segredos que viram o Secret oficina-execucao-secret (criados pelo oficina-infra-k8s;
  # ARN por padrao de nome por causa do sufixo aleatorio do Secrets Manager).
  statement {
    sid     = "SegredosLeitura"
    effect  = "Allow"
    actions = ["secretsmanager:GetSecretValue"]
    resources = [
      "arn:aws:secretsmanager:${var.region}:${local.conta}:secret:${var.project}/jwt_secret-*",
      "arn:aws:secretsmanager:${var.region}:${local.conta}:secret:${var.project}/newrelic_license_key-*",
    ]
  }
}

resource "aws_iam_role_policy" "deploy" {
  name   = "${local.nome_role_deploy}-policy"
  role   = aws_iam_role.gha_deploy.id
  policy = data.aws_iam_policy_document.deploy.json
}

# Acesso ao cluster RESTRITO aos namespaces dos ambientes (spec secao 5). Por isso o
# cd.yml nao faz chamadas cluster-scoped (nem cria Namespace: sao do oficina-app).
resource "aws_eks_access_entry" "gha_deploy" {
  cluster_name  = local.cluster_name
  principal_arn = aws_iam_role.gha_deploy.arn
  type          = "STANDARD"
}

resource "aws_eks_access_policy_association" "gha_deploy" {
  cluster_name  = local.cluster_name
  principal_arn = aws_iam_role.gha_deploy.arn
  policy_arn    = "arn:aws:eks::aws:cluster-access-policy/AmazonEKSAdminPolicy"

  access_scope {
    type       = "namespace"
    namespaces = ["oficina-prd", "oficina-hml"]
  }

  depends_on = [aws_eks_access_entry.gha_deploy]
}
