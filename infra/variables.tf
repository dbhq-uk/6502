variable "cloudflare_api_token" {
  description = "Cloudflare API token. From TF_VAR_cloudflare_api_token, sourced from 1Password by ~/.dbhq/env.sh. Never has a default."
  type        = string
  sensitive   = true
}

variable "account_id" {
  description = "Cloudflare account id. An identifier, not a credential."
  type        = string
  default     = "691c21cdcf1b3fa4add70cc166e99733"
}

variable "zone_id" {
  description = "Zone id for dbhq.uk. The zone itself is managed by ../terraform/; this project only adds a record to it."
  type        = string
  default     = "48bb46832a2853526a1082accdac4147"
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
