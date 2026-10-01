# infra

The infrastructure behind `6502.dbhq.uk`: a Cloudflare Pages project, its custom
domain, and one DNS record in the `dbhq.uk` zone.

```bash
source ~/.dbhq/env.sh                     # Cloudflare + R2 keys, from 1Password
export TF_VAR_cloudflare_api_token="$CLOUDFLARE_API_TOKEN"
export TF_VAR_account_id="$CLOUDFLARE_ACCOUNT_ID"
export TF_VAR_zone_id="$CLOUDFLARE_ZONE_ID"
terraform init -backend-config=backend.hcl \
  -backend-config="endpoints={s3=\"https://$CLOUDFLARE_ACCOUNT_ID.r2.cloudflarestorage.com\"}"
terraform plan
```

## Where it lives, and why

Here, beside the site, the way `modem` and `bbs` do it: the site in `../site/`,
this folder, and the deploy workflow in `../.github/workflows/deploy-site.yml`.
It was first drafted in the private DBHQ repository on the `terraken` split,
which exists because terraken's site tests read its Go source and cannot cross a
repository boundary. Nothing in the infrastructure needs that, so it moved.

The zone itself, and everything else in it, belongs to the DBHQ repository. This
configuration only adds a record, which is why `zone_id` is a plain variable
rather than a resource reference.

## State is this project's alone

The state lives in the R2 bucket `dbhq-6502-tfstate`, never on disk and never in
git. One bucket per site. The resources that were first put into a shared state
came back in a plan from a different project proposing to destroy them, and
separate state makes that impossible rather than merely unlikely.

**The R2 token has to be told about a new bucket.** The token `dbhq - R2
terraform state` is scoped to the state buckets by name, so a bucket it has not
been told about answers `terraform init` with a bare 403 on `HeadObject`. Add the
bucket to the token in the Cloudflare dashboard, under R2, Manage API tokens.

## Deployments are not managed here

The Pages project is direct-upload. The deploy workflow drives wrangler, and
Cloudflare serves exactly what it last uploaded. Terraform owns the project's
existence and its custom domain, not its contents, which is why `main.tf` has
`ignore_changes` on the deploy config.

## Applying this is a manual step, never CI

No workflow runs Terraform.

## The first apply, as it actually went (30 September 2026)

Applied in the order below, with one deliberate change: the DNS record was
published while the Pages custom domain was still `pending`, not after it was
`active`. The domain does not leave `pending` until its CNAME exists, so waiting
for it would never end. What made that safe is that the zone already has an
active universal certificate for `*.dbhq.uk`, so the proxied hostname served a
valid certificate from its first request and HSTS had nothing to refuse. It
answered 522 for about thirty seconds, then 200. Check the zone's certificate
packs before relying on this for a hostname that is not first-level.

The real `pages.dev` subdomain came back as `6502.pages.dev`: the bare name was
free, so the default `var.pages_target` was right.

## The order of the first apply is not the order Terraform would pick

The `dbhq.uk` zone sends HSTS with `includeSubDomains`, so this hostname must
serve valid HTTPS from its very first request. Publishing the DNS record before
there is a deployed site with a certificate fails for every browser that has seen
`dbhq.uk`, with no way to click past it.

1. The R2 bucket `dbhq-6502-tfstate` exists and is on the state token's list.
2. `terraform apply -target=cloudflare_pages_project.site`. If Cloudflare refuses
   `6502` as a project name because it starts with a digit, change
   `var.pages_project` here and the `--project-name` in the workflow together.
3. Upload something real. From `site/`, with `results.json` made from a test run:
   `npm ci && npm test && npm exec --no -- wrangler pages deploy dist
   --project-name 6502 --branch main`.
4. `terraform apply -target=cloudflare_pages_domain.site`. This is what makes
   Cloudflare issue the certificate.
5. Read the project's **real** `pages.dev` subdomain back and put it in
   `var.pages_target`. `6502.pages.dev` is a guess until then.
   ```bash
   curl -s -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
     "https://api.cloudflare.com/client/v4/accounts/$CLOUDFLARE_ACCOUNT_ID/pages/projects/6502" \
     | jq -r .result.subdomain
   ```
6. `terraform apply`.

## The deploy workflow's credentials

This repository is public, so its workflow needs a token of its own, named
`6502 repo - Pages deploy + cache purge`, with two permissions and no more: Pages
Write on the account and Cache Purge on the zone. Never the account-wide token.
Add it to the repository as `CLOUDFLARE_API_TOKEN`, with `CLOUDFLARE_ACCOUNT_ID`
and `CLOUDFLARE_ZONE_ID`, then set the repository variable `SITE_DEPLOY` to
`true`. Until it is `true`, the workflow is skipped rather than red.

## Analytics and Search Console

GA4 uses the estate's one measurement ID and needs nothing here. In Search
Console, add `https://6502.dbhq.uk/` under the `sc-domain:dbhq.uk` roll-up, grant
the service account access on it in the interface (the API cannot), and submit
`https://6502.dbhq.uk/sitemap-index.xml` on the roll-up.

## Secrets

The API token is the only credential here, supplied as `TF_VAR_cloudflare_api_token`
from 1Password and never written down. The account id and zone id are
identifiers, not credentials, but this repository is public and has no need to
publish them, so they have no defaults: they come from the environment the same
way (`TF_VAR_account_id`, `TF_VAR_zone_id`), and the R2 endpoint, which contains
the account id, is passed at init. Hostnames and the `pages.dev` target are
public anyway.
