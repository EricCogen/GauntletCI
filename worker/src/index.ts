import { SignJWT, importPKCS8, importSPKI, jwtVerify } from "jose";

export interface Env {
  // KV namespace for subscription status
  LICENSE_STORE: KVNamespace;
  // Vars (wrangler.toml)
  PRICE_ID_PRO: string;
  PRICE_ID_TEAMS: string;
  PRICE_ID_ENTERPRISE: string;
  FROM_EMAIL: string;
  REPLY_TO: string;
  GAUNTLETCI_PUBLIC_KEY: string;
  // Secrets (wrangler secret put)
  STRIPE_WEBHOOK_SECRET: string;
  GAUNTLETCI_PRIVATE_KEY: string;
  RESEND_API_KEY: string;
}

type Tier = "pro" | "teams" | "enterprise";

interface SubRecord {
  active: boolean;
  tier: Tier;
  updatedAt: number;
}

function priceToTier(priceId: string, env: Env): Tier | null {
  if (priceId === env.PRICE_ID_PRO) return "pro";
  if (priceId === env.PRICE_ID_TEAMS) return "teams";
  if (priceId === env.PRICE_ID_ENTERPRISE) return "enterprise";
  return null;
}

// ---- Routing ----------------------------------------------------------------

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/license/status") {
      return handleLicenseStatus(request, env);
    }

    if (request.method === "POST") {
      return handleStripeWebhook(request, env);
    }

    return new Response("Not found", { status: 404 });
  },
};

// ---- GET /license/status ----------------------------------------------------

async function handleLicenseStatus(request: Request, env: Env): Promise<Response> {
  const auth = request.headers.get("Authorization");
  if (!auth?.startsWith("Bearer ")) {
    return Response.json({ valid: false, reason: "missing_token" }, { status: 401 });
  }
  const token = auth.slice(7);

  let email: string;
  let tier: string;
  try {
    const publicKey = await importSPKI(env.GAUNTLETCI_PUBLIC_KEY, "RS256");
    const { payload } = await jwtVerify(token, publicKey, { issuer: "gauntletci.com" });
    email = payload["email"] as string;
    tier  = payload["tier"]  as string;
    if (!email || !tier) throw new Error("missing claims");
  } catch {
    return Response.json({ valid: false, reason: "invalid_token" }, { status: 401 });
  }

  const record = await getSubRecord(email, env);
  if (record && !record.active) {
    return Response.json({ valid: false, reason: "subscription_cancelled", tier: "community" });
  }

  // No KV record means the token was issued before KV was added -- trust the JWT.
  return Response.json({ valid: true, tier: record?.tier ?? tier });
}

// ---- KV helpers -------------------------------------------------------------

async function getSubRecord(email: string, env: Env): Promise<SubRecord | null> {
  const raw = await env.LICENSE_STORE.get(`sub:${email}`);
  return raw ? (JSON.parse(raw) as SubRecord) : null;
}

async function setSubRecord(email: string, record: SubRecord, env: Env): Promise<void> {
  await env.LICENSE_STORE.put(`sub:${email}`, JSON.stringify(record));
}

async function setCustomerEmail(customerId: string, email: string, env: Env): Promise<void> {
  await env.LICENSE_STORE.put(`cust:${customerId}`, email);
}

async function getEmailByCustomerId(customerId: string, env: Env): Promise<string | null> {
  return env.LICENSE_STORE.get(`cust:${customerId}`);
}

// ---- POST /stripe/webhook ---------------------------------------------------

async function handleStripeWebhook(request: Request, env: Env): Promise<Response> {
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

  switch (event.type) {
    case "checkout.session.completed":
      return handleCheckoutCompleted(event.data.object, env);
    case "invoice.paid":
      return handleInvoicePaid(event.data.object, env);
    case "customer.subscription.deleted":
      return handleSubscriptionDeleted(event.data.object, env);
    case "invoice.payment_failed":
      return handlePaymentFailed(event.data.object, env);
    default:
      return new Response("OK", { status: 200 });
  }
}

async function handleCheckoutCompleted(
  session: Record<string, unknown>,
  env: Env
): Promise<Response> {
  const email = extractSessionEmail(session);
  const priceId = extractSessionPriceId(session);
  const customerId = session["customer"] as string | undefined;

  if (!email) {
    console.error("No customer email in checkout session", JSON.stringify(session));
    return new Response("Missing email", { status: 400 });
  }
  if (!priceId) {
    console.error("No price ID in checkout session", JSON.stringify(session));
    return new Response("Missing price ID", { status: 400 });
  }

  const tier = priceToTier(priceId, env);
  if (!tier) {
    console.error(`Unknown price ID: ${priceId}`);
    return new Response(`Unknown price: ${priceId}`, { status: 400 });
  }

  // Store subscription status and customer->email mapping.
  await setSubRecord(email, { active: true, tier, updatedAt: Date.now() }, env);
  if (customerId) await setCustomerEmail(customerId, email, env);

  console.log(`Issuing ${tier} license for ${email}`);
  try {
    const token = await issueLicenseToken(email, tier, env);
    await sendLicenseEmail(email, tier, token, env);
    console.log(`License delivered to ${email} (${tier})`);
  } catch (err) {
    console.error("License issuance failed:", err);
    return new Response("Internal error", { status: 500 });
  }

  return new Response("OK", { status: 200 });
}

async function handleInvoicePaid(
  invoice: Record<string, unknown>,
  env: Env
): Promise<Response> {
  const email = invoice["customer_email"] as string | undefined;
  const customerId = invoice["customer"] as string | undefined;
  const priceId = extractInvoicePriceId(invoice);

  const resolvedEmail = email ?? (customerId ? await getEmailByCustomerId(customerId, env) : null);
  if (!resolvedEmail) {
    console.warn("invoice.paid: could not resolve customer email", customerId);
    return new Response("OK", { status: 200 });
  }

  const tier = priceId ? priceToTier(priceId, env) : null;
  const existing = await getSubRecord(resolvedEmail, env);
  await setSubRecord(resolvedEmail, {
    active: true,
    tier: tier ?? existing?.tier ?? "pro",
    updatedAt: Date.now(),
  }, env);

  console.log(`Subscription renewed for ${resolvedEmail}`);
  return new Response("OK", { status: 200 });
}

async function handleSubscriptionDeleted(
  subscription: Record<string, unknown>,
  env: Env
): Promise<Response> {
  const customerId = subscription["customer"] as string | undefined;
  if (!customerId) {
    console.warn("subscription.deleted: no customer ID");
    return new Response("OK", { status: 200 });
  }

  const email = await getEmailByCustomerId(customerId, env);
  if (!email) {
    console.warn(`subscription.deleted: no email found for customer ${customerId}`);
    return new Response("OK", { status: 200 });
  }

  const existing = await getSubRecord(email, env);
  await setSubRecord(email, {
    active: false,
    tier: existing?.tier ?? "pro",
    updatedAt: Date.now(),
  }, env);

  console.log(`Subscription cancelled for ${email}`);
  return new Response("OK", { status: 200 });
}

async function handlePaymentFailed(
  invoice: Record<string, unknown>,
  env: Env
): Promise<Response> {
  // Payment failed -- keep the subscription active until Stripe gives up retrying.
  // Stripe will fire subscription.deleted after all retries are exhausted.
  // Just log for now.
  const email = invoice["customer_email"] as string | undefined;
  console.warn(`Payment failed for ${email ?? "unknown"}`);
  return new Response("OK", { status: 200 });
}

// ---- Stripe helpers ---------------------------------------------------------

function extractSessionEmail(session: Record<string, unknown>): string | undefined {
  return (session["customer_email"] as string | undefined)
    ?? (session["customer_details"] as { email?: string } | undefined)?.email;
}

function extractSessionPriceId(session: Record<string, unknown>): string | undefined {
  return (
    (session["line_items"] as { data: { price: { id: string } }[] } | undefined)
      ?.data?.[0]?.price?.id
  );
}

function extractInvoicePriceId(invoice: Record<string, unknown>): string | undefined {
  return (
    (invoice["lines"] as { data: { price: { id: string } }[] } | undefined)
      ?.data?.[0]?.price?.id
  );
}

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

  if (expected.length !== signature.length) return false;
  let diff = 0;
  for (let i = 0; i < expected.length; i++) {
    diff |= expected.charCodeAt(i) ^ signature.charCodeAt(i);
  }
  return diff === 0;
}

// ---- License issuance -------------------------------------------------------

async function issueLicenseToken(email: string, tier: Tier, env: Env): Promise<string> {
  const privateKey = await importPKCS8(env.GAUNTLETCI_PRIVATE_KEY, "RS256");
  const now = Math.floor(Date.now() / 1000);
  const exp = now + 395 * 24 * 60 * 60; // ~13 months; renewed annually via invoice.paid

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
    "  Option 1 -- save to the default location:",
    "    mkdir -p ~/.gauntletci",
    `    echo '<token>' > ~/.gauntletci/gauntletci.key`,
    "",
    "  Option 2 -- set as an environment variable:",
    "    export GAUNTLETCI_LICENSE='<token>'",
    "",
    "  For CI/CD (GitHub Actions example):",
    "    Set a repository secret named GAUNTLETCI_LICENSE with the token value.",
    "    The CLI reads it automatically.",
    "",
    "Verify your license:",
    "    gauntletci license status",
    "",
    "Documentation:",
    "    https://gauntletci.com/docs",
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
