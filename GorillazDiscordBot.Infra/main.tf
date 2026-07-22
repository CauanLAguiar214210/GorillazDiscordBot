terraform {
  required_version = ">= 1.5"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

module "networking" {
  source      = "./AWS/networking"
  project     = var.project_name
  vpc_cidr    = var.vpc_cidr
  subnet_cidr = var.subnet_cidr
}

module "ecr" {
  source  = "./AWS/ecr"
  project = var.project_name
}

module "secrets" {
  source                       = "./AWS/secrets"
  project                      = var.project_name
  discord_token_placeholder    = var.discord_token
  owm_api_key_placeholder      = var.owm_api_key
  mongodb_connection_placeholder = var.mongodb_connection_string
}

module "ecs" {
  source             = "./AWS/ecs"
  project            = var.project_name
  vpc_id             = module.networking.vpc_id
  public_subnet_ids  = module.networking.public_subnet_ids
  security_group_id  = module.networking.security_group_id
  ecr_repository_url = module.ecr.repository_url
  discord_token_arn  = module.secrets.discord_token_arn
  owm_api_key_arn    = module.secrets.owm_api_key_arn
  mongodb_conn_arn   = module.secrets.mongodb_connection_arn
  mongodb_database   = var.mongodb_database
  command_prefix     = var.command_prefix
  task_cpu           = var.task_cpu
  task_memory        = var.task_memory
}
