# The infrastructure behind 6502.dbhq.uk.
#
# THE INFRASTRUCTURE LIVES WITH THE SITE, the way modem's and bbs's does: this
# folder, the site in ../site/ and the deploy workflow in
# ../.github/workflows/deploy-site.yml are all in this repository. The zone
# itself belongs to the DBHQ repository, which is why zone_id is a plain
# variable and this configuration only ever adds one record to it.
#
# The boundary is the one infra/skills draws in the DBHQ repository: Terraform
# owns the Pages project's existence and its custom domain, never its contents.
# The deploy workflow drives wrangler against the project this creates.
#
# WHAT IS AND IS NOT A SECRET, because the distinction decides what may live
# in a public repository and getting it wrong in either direction is
# expensive.
#
#   NOT SECRET, and deliberately committed:
#     account id, zone id, hostnames, the pages.dev target. These are
#     identifiers. They appear in every API call the account makes, and
#     treating an identifier as a secret buys nothing while costing the reader
#     the ability to understand what is deployed.
#
#   SECRET, and never in this repository in any form:
#     the API token. Supplied as TF_VAR_cloudflare_api_token at apply time,
#     from 1Password.
#
#   THE ACTUAL RISK, which is neither of those:
#     terraform state. It must never be in git, public OR private. It lives in
#     this project's own R2 bucket - see backend.hcl for why its own.
#
# THE ZONE RUNS HSTS WITH includeSubDomains, so this hostname has to serve
# valid HTTPS from its very first request. A browser that has already seen
# dbhq.uk refuses to load it over plain HTTP and refuses it on a certificate
# error, and it offers the reader no click-through past either. That rules out
# ever parking this subdomain on anything without a certificate, and it
# dictates the order of the first apply, which is not the order `terraform
# apply` would pick on its own:
#
#   1. terraform apply -target=cloudflare_pages_project.site
#   2. with results.json made from a test run:
#        cd site && npm ci && npm test
#        npm exec --no -- wrangler pages deploy dist --project-name 6502 --branch main
#   3. terraform apply -target=cloudflare_pages_domain.site
#      - this is what makes Cloudflare issue the certificate
#   4. read the project's REAL pages.dev subdomain back from the API and put it
#      in var.pages_target. Cloudflare appends a suffix when the bare project
#      name is taken globally, which is why skills is skills-bd9, modem is
#      modem-9e4 and bbs is bbs-57t. The suffix is not derivable, and a CNAME
#      pointing at a host that does not exist simply never resolves - so the
#      failure reads as a DNS problem rather than a naming one.
#   5. terraform apply
#
# Creating the DNS record before there is a deployed site with a certificate
# publishes a hostname that HSTS then refuses to load, and the failure is
# sticky in every browser that saw it: no HTTP fallback, no "proceed anyway",
# no reading the site while a certificate reissues. It is not permanent -
# service returns the moment the hostname serves valid HTTPS again - but it
# cannot be patched under load, only fixed properly and then waited on.

terraform {
  required_version = ">= 1.6"

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.20"
    }
  }

  # Configured at init from backend.hcl:
  #   terraform init -backend-config=backend.hcl
  backend "s3" {}
}

provider "cloudflare" {
  # From TF_VAR_cloudflare_api_token. Never a default, never written here.
  api_token = var.cloudflare_api_token
}

resource "cloudflare_pages_project" "site" {
  account_id        = var.account_id
  name              = var.pages_project
  production_branch = "main"

  lifecycle {
    # Deploy config is managed out of band, by the deploy
    # workflow in this repository driving wrangler. Terraform owns the project's existence and
    # its custom domain, not its deployments.
    ignore_changes = [build_config, deployment_configs, source]
  }
}

resource "cloudflare_pages_domain" "site" {
  account_id   = var.account_id
  project_name = cloudflare_pages_project.site.name
  domain       = var.hostname
}

# Proxied, like skills and modem and unlike heliograph. heliograph is on GitHub
# Pages, which cannot complete its certificate challenge through Cloudflare's
# proxy; Cloudflare Pages has no such problem, and proxying is what puts the
# zone's own headers and caching in front of it.
resource "cloudflare_record" "site" {
  zone_id = var.zone_id
  name    = "6502"
  type    = "CNAME"
  content = var.pages_target
  proxied = true
  ttl     = 1
  comment = "6502.dbhq.uk (Cloudflare Pages, project: 6502)"

  # Belt and braces on the ordering above: the record cannot be created until
  # the custom domain exists, which is what makes Cloudflare issue the
  # certificate. Terraform would otherwise be free to create them in parallel,
  # and under HSTS losing that race is expensive.
  depends_on = [cloudflare_pages_domain.site]
}
