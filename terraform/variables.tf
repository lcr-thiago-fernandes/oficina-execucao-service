variable "region" {
  description = "Regiao AWS."
  type        = string
  default     = "us-east-1"
}

variable "project" {
  description = "Prefixo dos recursos e dos caminhos no SSM (mesmo dos outros repositorios)."
  type        = string
  default     = "oficina"
}

variable "github_owner" {
  description = "Owner do repositorio GitHub autorizado a assumir a role de deploy."
  type        = string
  default     = "lcr-thiago-fernandes"
}

variable "github_repo" {
  description = "Repositorio (sem owner) que assume a role de deploy deste servico."
  type        = string
  default     = "oficina-execucao-service"
}

variable "ecr_keep_last_images" {
  description = "Quantas imagens manter no ECR (lifecycle policy)."
  type        = number
  default     = 10
}
