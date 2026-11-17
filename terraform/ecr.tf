# Copia do ecr.tf do oficina-infra-k8s, com repositorio proprio.
resource "aws_ecr_repository" "execucao" {
  name                 = local.nome_servico
  image_tag_mutability = "MUTABLE" # tag = SHA do commit (imutavel na pratica); MUTABLE permite retag manual na demo.
  force_delete         = true      # demo: destroy mesmo com imagens.

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_lifecycle_policy" "execucao" {
  repository = aws_ecr_repository.execucao.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Manter apenas as ultimas ${var.ecr_keep_last_images} imagens"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = var.ecr_keep_last_images
      }
      action = { type = "expire" }
    }]
  })
}
