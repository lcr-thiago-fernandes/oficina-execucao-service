# O NLB interno, os SGs do NLB e do VPC Link e o SG dos nos sao do oficina-infra-k8s
# (lidos do SSM). Este repositorio pendura um target group + listener por ambiente e as
# regras de SG das SUAS portas, como recursos separados (nunca inline: os SGs nao sao daqui).
#
# Cadeia (mesmo desenho do infra-k8s, decisao D4):
#   sg-vpclink --(8083)--> sg-nlb --(30084/30085)--> sg-nodes (EKS)
#   CIDR da VPC --(8084)--> sg-nlb   (hml so de dentro da VPC, decisao D3)

resource "aws_lb_target_group" "execucao" {
  for_each = local.ambientes

  name        = "${local.nome_servico}-${each.key}"
  port        = each.value.nodeport
  protocol    = "TCP"
  target_type = "instance"
  vpc_id      = data.aws_vpc.oficina.id

  preserve_client_ip   = "false"
  deregistration_delay = 30

  # Service NodePort com externalTrafficPolicy Cluster: qualquer no responde; o health
  # check em /health acompanha a readiness do servico (mesmos valores do infra-k8s).
  health_check {
    protocol            = "HTTP"
    path                = "/health"
    port                = "traffic-port"
    matcher             = "200"
    interval            = 10
    healthy_threshold   = 2
    unhealthy_threshold = 2
  }
}

resource "aws_lb_listener" "execucao" {
  for_each = local.ambientes

  load_balancer_arn = local.nlb_arn
  port              = each.value.porta_listener
  protocol          = "TCP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.execucao[each.key].arn
  }
}

# Cada ASG do node group x cada ambiente: novos nos entram sozinhos nos target groups.
locals {
  anexos_asg = {
    for par in setproduct(sort(tolist(local.node_asg_names)), keys(local.ambientes)) :
    "${par[1]}-${par[0]}" => { asg = par[0], ambiente = par[1] }
  }
}

resource "aws_autoscaling_attachment" "execucao" {
  for_each = local.anexos_asg

  autoscaling_group_name = each.value.asg
  lb_target_group_arn    = aws_lb_target_group.execucao[each.value.ambiente].arn
}

# ---------- regras de SG ----------
# VPC Link -> NLB so no listener de PRODUCAO (o API Gateway integra so com prd).
resource "aws_vpc_security_group_egress_rule" "vpclink_para_nlb" {
  security_group_id            = local.vpclink_sg_id
  description                  = "Listener prd do oficina-execucao no NLB"
  ip_protocol                  = "tcp"
  from_port                    = local.ambientes.prd.porta_listener
  to_port                      = local.ambientes.prd.porta_listener
  referenced_security_group_id = local.nlb_sg_id
}

resource "aws_vpc_security_group_ingress_rule" "nlb_recebe_do_vpclink" {
  security_group_id            = local.nlb_sg_id
  description                  = "oficina-execucao prd a partir do VPC Link"
  ip_protocol                  = "tcp"
  from_port                    = local.ambientes.prd.porta_listener
  to_port                      = local.ambientes.prd.porta_listener
  referenced_security_group_id = local.vpclink_sg_id
}

# Homologacao nao passa pelo API Gateway (decisao D3): so de dentro da VPC.
resource "aws_vpc_security_group_ingress_rule" "nlb_hml_da_vpc" {
  security_group_id = local.nlb_sg_id
  description       = "oficina-execucao hml a partir da VPC"
  ip_protocol       = "tcp"
  from_port         = local.ambientes.hml.porta_listener
  to_port           = local.ambientes.hml.porta_listener
  cidr_ipv4         = data.aws_vpc.oficina.cidr_block
}

resource "aws_vpc_security_group_egress_rule" "nlb_para_nos" {
  for_each = local.ambientes

  security_group_id            = local.nlb_sg_id
  description                  = "NodePort oficina-execucao ${each.key} (trafego e health check)"
  ip_protocol                  = "tcp"
  from_port                    = each.value.nodeport
  to_port                      = each.value.nodeport
  referenced_security_group_id = local.node_sg_id
}

resource "aws_vpc_security_group_ingress_rule" "nos_recebem_do_nlb" {
  for_each = local.ambientes

  security_group_id            = local.node_sg_id
  description                  = "NodePort oficina-execucao ${each.key} a partir do NLB interno"
  ip_protocol                  = "tcp"
  from_port                    = each.value.nodeport
  to_port                      = each.value.nodeport
  referenced_security_group_id = local.nlb_sg_id
}
