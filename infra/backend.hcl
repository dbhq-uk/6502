# Backend configuration for this configuration's own R2 state bucket.
#
# Committed on purpose. Every value here is an identifier, not a credential:
# a bucket name and a key. The keys that open it come
# from 1Password at init time as AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY,
# by way of `source ~/.dbhq/env.sh`, and are never written to disk.
#
# ONE BUCKET PER SITE, and this one is the 6502 site's alone. modem's three
# resources first went into the shared ../terraform/ state, and while they sat
# there a plan from a different project in the same state proposed destroying
# all three - they were present in the state and absent from the configuration
# on that branch. Separate state makes that impossible rather than merely
# unlikely: no other configuration can see these resources, so none of them
# can plan to remove them. skills, heliograph, bbs and modem are all laid out
# this way.
#
# Sharing a repo is not sharing a state. See README.md before changing this.
#
# THE R2 TOKEN HAS TO BE TOLD ABOUT THIS BUCKET. The token in 1Password
# ("dbhq - R2 terraform state") is scoped to the state buckets by name, so
# creating the bucket is not enough: a fresh bucket the token has never been
# told about answers `terraform init` with a bare 403 on HeadObject, which does
# not name the cause. Add it in the Cloudflare dashboard under R2 > API.

bucket = "dbhq-6502-tfstate"
key    = "6502.tfstate"
region = "auto"

# The endpoint is not written here: it contains the account id, which this public
# repository has no need to publish. Pass it at init, from the environment:
#
#   terraform init -backend-config=backend.hcl \
#     -backend-config="endpoints={s3=\"https://$CLOUDFLARE_ACCOUNT_ID.r2.cloudflarestorage.com\"}"

# R2 is S3-compatible, not S3. Each of these switches off a check that assumes
# a real AWS endpoint on the other end, and each one fails the init without it.
skip_credentials_validation = true
skip_region_validation      = true
skip_requesting_account_id  = true
skip_s3_checksum            = true
use_path_style              = true
