# GauntletCI License Worker

Cloudflare Worker that handles Stripe `checkout.session.completed` webhooks,
generates a signed RS256 JWT license token, and emails it to the customer via Resend.

## Prerequisites

- [Cloudflare account](https://dash.cloudflare.com/sign-up) (free)
- [Wrangler CLI](https://developers.cloudflare.com/workers/wrangler/install-and-update/)
- [Resend account](https://resend.com) (free -- 100 emails/day)
- Stripe account with Payment Links configured

## Setup (one-time)

### 1. Install dependencies

```
cd worker
npm install
```

### 2. Fill in price IDs

After creating Payment Links in the Stripe dashboard, copy each price ID
(format: `price_...`) into `wrangler.toml`:

```toml
PRICE_ID_PRO        = "price_..."
PRICE_ID_TEAMS      = "price_..."
PRICE_ID_ENTERPRISE = "price_..."
```

### 3. Set secrets

Run each command and paste the value when prompted:

```
# From Stripe Dashboard > Developers > Webhooks > your endpoint > Signing secret
npx wrangler secret put STRIPE_WEBHOOK_SECRET

# Contents of gauntletci-signing.key (the RS256 PEM private key -- full file content)
npx wrangler secret put GAUNTLETCI_PRIVATE_KEY

# From https://resend.com/api-keys
npx wrangler secret put RESEND_API_KEY
```

### 4. Deploy

```
npm run deploy
```

Wrangler prints the Worker URL, e.g.:
```
https://gauntletci-license-worker.<your-subdomain>.workers.dev
```

### 5. Register the webhook in Stripe

Stripe Dashboard > Developers > Webhooks > Add endpoint:
- URL: `https://gauntletci-license-worker.<your-subdomain>.workers.dev`
- Events to listen for: `checkout.session.completed`

Copy the signing secret Stripe shows you and set it:
```
npx wrangler secret put STRIPE_WEBHOOK_SECRET
```

### 6. Verify the domain (Resend)

In Resend, add and verify the `gauntletci.com` sending domain so emails
arrive from `licenses@gauntletci.com` rather than a shared Resend domain.

## Development

Run locally with a real Stripe test webhook:

```
npm run dev
# In another terminal:
stripe listen --forward-to localhost:8787
```

## Logs

Real-time log streaming:
```
npm run tail
```

For persistent logs, upgrade to the Cloudflare Workers Paid plan ($5/month).

## Security notes

- Stripe webhook signature is verified on every request (HMAC-SHA256, replay window 5 min).
- The RS256 private key never leaves Cloudflare's secret store.
- All secrets are set via `wrangler secret put` -- never committed to the repo.
- Failed license delivery returns HTTP 500 so Stripe automatically retries.
