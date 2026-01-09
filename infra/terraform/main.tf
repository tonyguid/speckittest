# Blob Copy Infrastructure - Terraform Configuration
# Version: 1.0.0
# Description: Azure infrastructure for Blob Copy application

terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

# Variables
variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "eastus"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "app_name" {
  description = "Application name"
  type        = string
  default     = "blob-copy"
}

variable "app_service_sku" {
  description = "App Service plan SKU"
  type        = string
  default     = "B2"
}

variable "app_insights_retention_days" {
  description = "Application Insights retention days"
  type        = number
  default     = 30
}

# Data sources
data "azurerm_client_config" "current" {}

data "azurerm_subscription" "current" {}

# Local variables
locals {
  resource_prefix        = "${var.app_name}-${var.environment}"
  storage_account_name   = "${var.app_name}${substr(md5(data.azurerm_subscription.current.id), 0, 8)}"
  
  tags = {
    environment = var.environment
    application = var.app_name
    createdAt   = timestamp()
  }
}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = "${local.resource_prefix}-rg"
  location = var.location
  tags     = local.tags
}

# Storage Account
resource "azurerm_storage_account" "main" {
  name                     = local.storage_account_name
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"
  access_tier              = "Hot"
  
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = true
  min_tls_version                 = "TLS1_2"
  
  network_rules {
    default_action             = "Allow"
    bypass                     = ["AzureServices"]
  }
  
  tags = local.tags
}

# Key Vault
resource "azurerm_key_vault" "main" {
  name                       = "${local.resource_prefix}-kv"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  
  enabled_for_deployment          = true
  enabled_for_template_deployment = true
  enabled_for_disk_encryption     = false
  
  tags = local.tags
}

# Key Vault Secret - Storage Account Connection String
resource "azurerm_key_vault_secret" "storage_connection" {
  name         = "StorageAccountConnectionString"
  value        = "DefaultEndpointsProtocol=https;AccountName=${azurerm_storage_account.main.name};AccountKey=${azurerm_storage_account.main.primary_access_key};EndpointSuffix=core.windows.net"
  key_vault_id = azurerm_key_vault.main.id
}

# Application Insights
resource "azurerm_application_insights" "main" {
  name                = "${local.resource_prefix}-ai"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  application_type    = "web"
  retention_in_days   = var.app_insights_retention_days
  
  tags = local.tags
}

# User Assigned Managed Identity for Backend
resource "azurerm_user_assigned_identity" "backend" {
  name                = "${local.resource_prefix}-backend-identity"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  
  tags = local.tags
}

# App Service Plan
resource "azurerm_service_plan" "main" {
  name                = "${local.resource_prefix}-asp"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  worker_count        = 2
  
  tags = local.tags
}

# Static Web App
resource "azurerm_static_web_app" "main" {
  name                = "${local.resource_prefix}-swa"
  resource_group_name = azurerm_resource_group.main.name
  location            = "westus2" # Static Web Apps available in limited regions
  sku_tier            = "Standard"
  sku_size            = "Standard"
  
  tags = local.tags
}

# App Service (Backend API)
resource "azurerm_linux_web_app" "main" {
  name                = "${local.resource_prefix}-api"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true
  
  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.backend.id]
  }
  
  site_config {
    always_on              = true
    minimum_tls_version    = "1.2"
    http2_enabled          = true
    application_stack {
      dotnet_version = "10.0"
    }
    
    cors {
      allowed_origins = [
        "http://localhost:3000",
        "https://${azurerm_static_web_app.main.default_host_name}"
      ]
      support_credentials = false
    }
  }
  
  app_settings = {
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE"       = "false"
    "ASPNETCORE_ENVIRONMENT"                    = var.environment
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~3"
    "APPINSIGHTS_INSTRUMENTATIONKEY"            = azurerm_application_insights.main.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING"     = azurerm_application_insights.main.connection_string
    "KeyVaultName"                              = azurerm_key_vault.main.name
    "ManagedIdentityClientId"                   = azurerm_user_assigned_identity.backend.client_id
  }
  
  connection_string {
    name  = "ApplicationInsights"
    type  = "Custom"
    value = azurerm_application_insights.main.connection_string
  }
  
  tags = local.tags
}

# Static Web App Custom Domain (conditional for prod environment)
resource "azurerm_static_web_app_custom_domain" "main" {
  count               = var.environment == "prod" ? 1 : 0
  static_web_app_id   = azurerm_static_web_app.main.id
  domain_name         = "blob-copy.example.com"
  validation_type     = "cname-delegation"
}

# Outputs
output "storage_account_name" {
  description = "Storage account name"
  value       = azurerm_storage_account.main.name
}

output "storage_account_id" {
  description = "Storage account ID"
  value       = azurerm_storage_account.main.id
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = azurerm_key_vault.main.name
}

output "key_vault_id" {
  description = "Key Vault ID"
  value       = azurerm_key_vault.main.id
}

output "app_service_name" {
  description = "App Service name"
  value       = azurerm_linux_web_app.main.name
}

output "app_service_url" {
  description = "App Service URL"
  value       = "https://${azurerm_linux_web_app.main.default_hostname}"
}

output "static_web_app_url" {
  description = "Static Web App URL"
  value       = "https://${azurerm_static_web_app.main.default_host_name}"
}

output "app_insights_instrumentation_key" {
  description = "Application Insights instrumentation key"
  value       = azurerm_application_insights.main.instrumentation_key
  sensitive   = true
}

output "app_insights_connection_string" {
  description = "Application Insights connection string"
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

output "backend_identity_principal_id" {
  description = "Backend managed identity principal ID"
  value       = azurerm_user_assigned_identity.backend.principal_id
}

output "backend_identity_client_id" {
  description = "Backend managed identity client ID"
  value       = azurerm_user_assigned_identity.backend.client_id
}
