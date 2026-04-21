import { SignJWT, importPKCS8 } from "jose";

export interface Env {
  // Vars (wrangler.toml)
  PRICE_ID_PRO: string;
  PRICE_ID_TEAMS: string;
  PRICE_ID_ENTERPRISE: string;
  FROM_EMAIL: string;
  REPLY_TO: string;
  // Secrets (wrangler secret put)
  STRIPE_WEBHOOK_SECRET: string;
  GAUNTLETCI_PRIVATE_KEY: string;
  RESEND_API_KEY: string;
}

type Tier = "pro" | "teams" | "enterprise";

function priceToTier(priceId: string, env: Env): Tier | null {
  if (priceId === env.PRICE_ID_PRO) return "pro";
  if (priceId === env.PRICE_ID_TEAMS) return "teams";
  if (priceId === env.PRICE_ID_ENTERPRISE) return "enterprise";
  return null;
}

// Verify Stripe webhook signature using HMAC-SHA256.
// Stripe signs with: "v1=" + HMAC_SHA256(secret, timestamp + "." + body)
async function verifyStripeSignature(
  body: string,
  sigHeader: string,
  secret: string
): Promise<boolean> {
  const parts = Object.fromEntries(
    sigHeader.split(",").map((p) => p.split("=") as [string, string])
  );
  const timestamp = parts["t"];
  const signature = parts["v1"];
  if (!timestamp || !signature) return false;

  // Reject webhooks older than 5 minutes to prevent replay attacks.
  const age = Math.floor(Date.now() / 1000) - parseInt(timestamp, 10);
  if (age > 300) return false;

  const payload = `${timestamp}.${body}`;
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  const mac = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(payload));
  const expected = Array.from(new Uint8Array(mac))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");

  // Constant-time comparison to prevent timing attacks.
  if (expected.length !== signature.length) return false;
  let diff = 0;
  for (let i = 0; i < expected.length; i++) {
    diff |= expected.charCodeAt(i) ^ signature.charCodeAt(i);
  }
  return diff === 0;
}

async function issueLicenseToken(email: string, tier: Tier, env: Env): Promise<string> {
  const privateKey = await importPKCS8(env.GAUNTLETCI_PRIVATE_KEY, "RS256");
  const now = Math.floor(Date.now() / 1000);
  const exp = now + 365 * 24 * 60 * 60; // 1 year

  return new SignJWT({ email, tier, iss: "gauntletci.com" })
    .setProtectedHeader({ alg: "RS256" })
    .setIssuedAt(now)
    .setExpirationTime(exp)
    .setSubject(email)
    .sign(privateKey);
}

async function sendLicenseEmail(
  email: string,
  tier: Tier,
  token: string,
  env: Env
): Promise<void> {
  const tierLabel = tier.charAt(0).toUpperCase() + tier.slice(1);
  const body = [
    `Your GauntletCI ${tierLabel} license`,
    "",
    `Thank you for subscribing to GauntletCI ${tierLabel}.`,
    "",
    "Your license token:",
    "",
    token,
    "",
    "To activate:",
    "",
    `  Option 1 -- save to the default location:`,
    `    mkdir -p ~/.gauntletci`,
    `    echo '<token>' > ~/.gauntletci/gauntletci.key`,
    "",
    `  Option 2 -- set as an environment variable:`,
    `    export GAUNTLETCI_LICENSE='<token>'`,
    "",
    "Verify your license:",
    "    gauntletci license status",
    "",
    "Documentation:",
    "    https://gauntletci.com/docs",
    "",
    "Your license is valid for 12 months from today. You will receive",
    "a renewal reminder 30 days before it expires.",
    "",
    "Questions? Reply to this email or open an issue on GitHub.",
    "",
    "-- GauntletCI",
  ].join("\n").replace(/<token>/g, token);

  const res = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${env.RESEND_API_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      from: env.FROM_EMAIL,
      reply_to: env.REPLY_TO,
      to: [email],
      subject: `Your GauntletCI ${tierLabel} license`,
      text: body,
    }),
  });

  if (!res.ok) {
    const err = await res.text();
    throw new Error(`Resend API error ${res.status}: ${err}`);
  }
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (request.method !== "POST") {
      return new Response("Method not allowed", { status: 405 });
    }

    const sigHeader = request.headers.get("stripe-signature");
    if (!sigHeader) {
      return new Response("Missing stripe-signature header", { status: 400 });
    }

    const rawBody = await request.text();

    const valid = await verifyStripeSignature(rawBody, sigHeader, env.STRIPE_WEBHOOK_SECRET);
    if (!valid) {
      console.error("Stripe signature verification failed");
      return new Response("Invalid signature", { status: 400 });
    }

    let event: { type: string; data: { object: Record<string, unknown> } };
    try {
      event = JSON.parse(rawBody);
    } catch {
      return new Response("Invalid JSON", { status: 400 });
    }

    // Only handle completed checkouts -- acknowledge all others silently.
    if (event.type !== "checkout.session.completed") {
      return new Response("OK", { status: 200 });
    }

    const session = event.data.object;
    const email = session["customer_email"] as string | undefined
      ?? (session["customer_details"] as { email?: string } | undefined)?.email;
    const priceId = (
      (session["line_items"] as { data: { price: { id: string } }[] } | undefined)
        ?.data?.[0]?.price?.id
    );

    if (!email) {
      console.error("No customer email in session", JSON.stringify(session));
      return new Response("Missing email", { status: 400 });
    }

    if (!priceId) {
      console.error("No price ID in session", JSON.stringify(session));
      return new Response("Missing price ID", { status: 400 });
    }

    const tier = priceToTier(priceId, env);
    if (!tier) {
      console.error(`Unknown price ID: ${priceId}`);
      return new Response(`Unknown price: ${priceId}`, { status: 400 });
    }

    console.log(`Issuing ${tier} license for ${email}`);

    try {
      const token = await issueLicenseToken(email, tier, env);
      await sendLicenseEmail(email, tier, token, env);
      console.log(`License delivered to ${email} (${tier})`);
    } catch (err) {
      console.error("License issuance failed:", err);
      // Return 500 so Stripe retries the webhook.
      return new Response("Internal error", { status: 500 });
    }

    return new Response("OK", { status: 200 });
  },
};
