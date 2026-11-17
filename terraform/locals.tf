locals {
  # Dois ambientes no MESMO cluster (um namespace cada). NodePort e listener do NLB sao
  # unicos no cluster/NLB: 30080/30081 e 80/81 = oficina-app; 30082/30083 e 8081/8082 =
  # Billing. O API Gateway integra so com prd; hml so e alcancavel de dentro da VPC
  # (decisao D3 do oficina-infra-k8s).
  ambientes = {
    prd = { nodeport = 30084, porta_listener = 8083, namespace = "oficina-prd" }
    hml = { nodeport = 30085, porta_listener = 8084, namespace = "oficina-hml" }
  }

  nome_servico     = "${var.project}-execucao" # ECR, target groups, roles
  nome_role_deploy = "${var.project}-gha-deploy-execucao"

  # ServiceAccount do k8s/serviceaccount.yaml (anotada com a role IRSA do ambiente).
  service_account = "${var.project}-execucao"

  conta = data.aws_caller_identity.atual.account_id
}
