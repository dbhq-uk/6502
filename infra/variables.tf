variable "cloudflare_api_token" {
  description = "Cloudflare API token. From TF_VAR_cloudflare_api_token, sourced from 1Password by ~/.dbhq/env.sh. Never has a default."
  type        = string
  sensitive   = true
}

variable "account_id" {
  description = "Cloudflare account id. From TF_VAR_account_id, set from CLOUDFLARE_ACCOUNT_ID by ~/.dbhq/env.sh. No default: it is an identifier rather than a credential, but this repository is public and has no need to publish it."
  type        = string
}

variable "zone_id" {
  description = "Zone id for dbhq.uk. From TF_VAR_zone_id, set from CLOUDFLARE_ZONE_ID by ~/.dbhq/env.sh. No default, for the same reason as account_id. The zone itself belongs to the DBHQ repository; this project only adds a record to it."
  type        = string
}

variable "hostname" {
  description = "Where the site lives."
  type        = string
  default     = "6502.dbhq.uk"
}

variable "pages_project" {
  description = "Cloudflare Pages project name. The deploy workflow in this repository names this same string in its wrangler call, so the two have to agree."
  type        = string
  default     = "6502"
}

variable "pages_target" {
  description = "The pages.dev hostname the custom domain CNAMEs to. Cloudflare appends a suffix when the bare project name is already taken globally - skills is skills-bd9, modem is modem-9e4 and bbs is bbs-57t - so the value is a read-back result and not derivable. 6502.pages.dev is a GUESS until step 4 of the first apply has read the real subdomain back from the API; a CNAME pointing at a host that does not exist simply never resolves, so the failure reads as a DNS problem rather than a naming one."
  type        = string
  default     = "6502.pages.dev"
}
