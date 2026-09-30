output "site_url" {
  description = "Where the site is served."
  value       = "https://${var.hostname}"
}

output "pages_target" {
  description = "The pages.dev host the custom domain resolves to."
  value       = var.pages_target
}

output "pages_project" {
  description = "The Cloudflare Pages project the deploy workflow in this repository uploads to."
  value       = cloudflare_pages_project.site.name
}
