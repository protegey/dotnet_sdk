# Protegey.Sdk (.NET)

Official Protegey SDK for .NET backends. It covers the whole partner API (KYC sessions, KYB
applicants, 3D liveness, transaction monitoring, cash deposits, alerts, device and behavioral events, sanctions screening,
the shared fraud signal network) plus webhook signature verification. You call it directly from
your server with your own API key.

Backend-only: device fingerprinting and behavioral biometrics are *collected* on the client by
`@protegey/sdk` (JS), `protegey_sdk` (Flutter) or `@protegey/react-native-sdk`. This SDK can
forward what they collect (`DeviceEvents`, `BehavioralEvents`) from your server.

Targets .NET 8+. Zero external dependencies — built entirely on `System.Net.Http.HttpClient` and
`System.Text.Json`, both already in the base class library.

## Install

Not yet published to NuGet.org — install directly from GitHub for now:

```bash
git clone https://github.com/protegey/dotnet_sdk.git
cd dotnet_sdk
dotnet pack src/Protegey.Sdk -c Release
# then reference the produced .nupkg from src/Protegey.Sdk/bin/Release/ in your own project,
# e.g. via a local NuGet feed, or just add a ProjectReference to src/Protegey.Sdk if you're
# vendoring the source directly.
```

Source: [github.com/protegey/dotnet_sdk](https://github.com/protegey/dotnet_sdk)

## Usage

Every method maps 1:1 onto one `/partner-api` endpoint, takes plain dictionaries and returns the
parsed JSON as `Dictionary<string, object?>` (every number comes back as `double`, arrays as
`List<object?>`, objects as `Dictionary<string, object?>`). All validation stays server-side; a
non-2xx response throws `ProtegeyApiException` (`Status`, `Message`, and `ErrorCode` whenever the
body carries one — e.g. `PARTNER_SUSPENDED` / `PARTNER_CONTRACT_REQUIRED` on some 403s,
`NO_3D_ENROLLMENT` on a 400 from 3D liveness re-verification).

Authentication is the `x-api-key` header. **Sandbox vs live is decided only by which key you
pass** — there is no flag or header for it. Sandbox is capped at 100 transactions and 10 KYC
sessions per day (UTC). List methods are paginated: `page`, `limit` (1–100) in, `{data, total,
page, limit, totalPages}` out.

Status-like values (`status`, `submissionStatus`, `previousStatus`, `newStatus`, `decision`,
`decisionVerdict`, `disposition`, `deviceAction`, `action`, `category`, `outcome`,
`confidenceTier`, `ruleDecision`) come back in UPPER_SNAKE_CASE, in responses and webhooks alike.
Other enums (`ruleSeverity`, `priority`, `source`, `direction`, …) are returned as the API
stores them.

```csharp
using Protegey.Sdk;

var protegey = new ProtegeyClient("YOUR_API_KEY", "https://api.protegey.com");

// Transactions — returns {transactionId, decision, riskScore, deviceAction, alerts[]}
var result = await protegey.Transactions.ReportAsync(new Dictionary<string, object?>
{
    ["externalTransactionId"] = "tx-00234", // idempotency key — a duplicate answers 409
    ["externalCustomerId"] = "cust-9981",
    ["customerName"] = "Awa Diallo",
    ["direction"] = "DEBIT",                // exact uppercase
    ["amount"] = 250000,
    ["currency"] = "XOF",
    ["transactionType"] = "cashout",
    ["channel"] = "mobile_app",
    ["isCash"] = true,
    ["location"] = new Dictionary<string, object?> { ["latitude"] = 14.6928, ["longitude"] = -17.4467, ["accuracyMeters"] = 25 }, // optional GPS fix
    // occurredAt defaults to now when omitted
});
var page = await protegey.Transactions.ListAsync(new() { ["decision"] = "BLOCKED", ["limit"] = 50 });
var tx = await protegey.Transactions.GetAsync("tx-00234"); // Protegey id or your external id

// Cash deposits — returns {cashDepositId, externalTransactionId, decision, riskScore, alerts[]}
var deposit = await protegey.CashDeposits.RecordAsync(new Dictionary<string, object?>
{
    ["externalTransactionId"] = "dep-0001", // optional; makes retries safe — a duplicate answers 409
    ["externalCustomerId"] = "cust-9981",
    ["amount"] = 2500000,
    ["currency"] = "XOF",
    ["channel"] = "BRANCH",
    ["branchId"] = "DKR-PLATEAU",
    ["depositorName"] = "Moussa Traoré",    // who brought the cash, if not the account holder
    // depositedAt defaults to now when omitted
});
var deposits = await protegey.CashDeposits.ListAsync(new() { ["dateFrom"] = "2026-11-01", ["minAmount"] = 1000000, ["channel"] = "BRANCH" });
var one = await protegey.CashDeposits.GetAsync("dep-0001"); // Protegey id or your externalTransactionId
// one["amount"] is a decimal string ("2500000.00"), not a number

// Alerts
var open = await protegey.Alerts.ListAsync(new() { ["status"] = "OPEN" }); // SDK lowercases the filter for you
await protegey.Alerts.UpdateAsync("alert-uuid", new() { ["status"] = "CONFIRMED", ["disposition"] = "CONFIRMED_FRAUD" });
var escalated = await protegey.Alerts.ConvertToCaseAsync("alert-uuid");     // {alert, case}
await protegey.Alerts.FlagTransactionAsync((string)result["transactionId"]!); // Protegey transactionId only

// Identity verification (KYC)
var session = await protegey.Kyc.StartSessionAsync("cust-9981", email: "awa@example.com");
// Send session["url"] (single-use, valid until session["expiresAt"] — 24 h by default) to your user however you like
var current = await protegey.Kyc.GetSessionAsync((string)session["sessionId"]!);
// current["status"] is the final decision; current["recommendation"] (APPROVE | REVIEW | REJECT | null)
// is Protegey's own verdict and never changes after a manual override.
// current["images"]: {documentFront, documentBack, selfie, documentFaceCrop}, each {url, expiresAt} or null —
// pre-signed links valid 1 hour, re-issued on every GET (don't store them).
var inReview = await protegey.Kyc.ListSessionsAsync(new() { ["status"] = "IN_REVIEW" });
var forCustomer = await protegey.Kyc.ListSessionsAsync(new() { ["externalUserId"] = "cust-9981" }); // exact match
await protegey.Kyc.UpdateSessionAsync((string)session["sessionId"]!, new() { ["status"] = "APPROVED", ["reviewNote"] = "Checked manually" });
var fresh = await protegey.Kyc.ResendSessionAsync((string)session["sessionId"]!);

// Business onboarding (KYB) — email OR phone (at least one)
var invite = await protegey.Kyb.InviteApplicantAsync("John Mbarga", contactPhone: "+237690000000");
// invite["applicationUrl"] — send it yourself (no email is sent when you only give a phone)
var applicants = await protegey.Kyb.ListApplicantsAsync(new() { ["stage"] = "responded" });
var screening = await protegey.Kyb.ScreenApplicantAsync((string)invite["id"]!);
await protegey.Kyb.DecideAsync((string)invite["id"]!, "REQUEST_MORE_INFO", note: "Please upload your licence");
await protegey.Kyb.ResendInvitationAsync((string)invite["id"]!); // returns the submission + new applicationUrl
var check = await protegey.Kyb.RequestKycVerificationAsync((string)invite["id"]!, email: "john@example.com"); // see below

// Sanctions / PEP screening — {decision, score, pepMatch, matches[], provider, providers, degraded}
var hit = await protegey.Sanctions.ScreenAsync("John Doe", "person");

// Shared fraud signal network — matches[] only carry {category, reportedDaysAgo}
var signal = await protegey.SharedSignal.CheckAsync(new() { ["phoneNumber"] = "+237690000000", ["externalCustomerId"] = "cust-9981" });

// Forward client-collected device / behavioral signals
await protegey.DeviceEvents.RecordAsync(new() { ["eventId"] = "evt-1", ["visitorId"] = "visitor-from-client-sdk", ["externalCustomerId"] = "cust-9981" });
await protegey.BehavioralEvents.RecordAsync(new() { ["externalCustomerId"] = "cust-9981", ["sessionId"] = "app-session-1" });
```

KYC capture links are single-use and valid until the returned `expiresAt` (`StartSessionAsync`,
`ResendSessionAsync`). The lifetime is set per account in the portal's KYC settings — 30 min, 1 h,
24 h, 3 days or 7 days, **24 h by default**.

### KYB identity check

Once a KYB applicant has submitted its application, ask the person behind it (the contact, or
e.g. a director) to verify their identity — a standard KYC session linked to the applicant:

```csharp
var kyc = await protegey.Kyb.RequestKycVerificationAsync(clientId,
    fullName: "Jane Sample",       // compared with the name on the ID document; defaults to the contact name
    email: "jane@example.com");    // Protegey emails the link ONLY when email is given
// kyc: {sessionId, kybClientId, url, expiresAt, emailedTo} — emailedTo is null without email: relay url yourself.
// The link is valid at least 7 days.
var checks = await protegey.Kyb.ListKycVerificationsAsync(clientId);
// checks["data"][]: {sessionId, status, outcome, recommendation, expectedName, documentFullName,
//                    nameMatch (MATCH | MISMATCH | null), livenessScore, faceMatchScore, livemode, createdAt, updatedAt}
```

`GetApplicantAsync` returns the same items as `kycVerifications`. The KYC session itself
(`Kyc.GetSessionAsync`, list, `kyc.status_changed`) carries `kybClientId` and `kybNameMatch`. A
400 means the applicant has not submitted its application yet.

Wire-format quirks the SDK handles for you: the KYC list `status` filter is sent in Title Case
(`"In Review"`), which every API version accepts, the alert list `status`/`disposition` filters and the sanctions `type` only
accept lowercase. KYB responses currently also include two internal fields (`tokenHash`,
`documentFilePath`); the SDK strips them.

## 3D liveness

A separate, paid product (billed per check where liveness was proven) — KYC is unaffected and
stays free. Two modes: `LIVENESS` (selfie-only 3D liveness check) and `REVERIFICATION` (liveness,
then a face match against the face of that customer's latest KYC verification). `mode` and
`status` (`PENDING` | `COMPLETED` | `EXPIRED`) are UPPERCASE on the wire.

```csharp
// Selfie-only liveness — externalUserId is optional
var check = await protegey.Liveness.StartSessionAsync("cust-9981");
// Send check["url"] (single-use, valid 30 minutes) to your user, or open it for them

// Re-verify a returning customer against their KYC face
try
{
    var reverify = await protegey.Liveness.StartReverificationAsync("cust-9981");
}
catch (ProtegeyApiException e) when (e.ErrorCode == LivenessErrorCodes.No3DEnrollment)
{
    // 400 — no KYC face on record for this user: run a KYC session first
}

var result = await protegey.Liveness.GetSessionAsync((string)check["sessionId"]!);
// result["livenessProven"] (bool or null), result["matchLevel"] (higher means a stronger match; 0 means a different person),
// result["matched"] (reverification only); billing: result["billableChecks"], result["amount"]
// (decimal string, USD), result["checks"][] per attempt
var history = await protegey.Liveness.ListSessionsAsync(new() { ["externalUserId"] = "cust-9981", ["mode"] = "REVERIFICATION" });
```

## Webhooks

Protegey `POST`s JSON to your HTTPS webhook URL with `X-Signature` (hex HMAC-SHA256 of
`"{X-Timestamp}.{rawBody}"` with your webhook secret) and `X-Timestamp` (unix seconds). A
delivery succeeds on any 2xx; failures are retried up to 5 times over about 3 hours, so
**deduplicate on `eventId`**. The event name is the payload's `type` (`WebhookEventTypes`):
`kyc.status_changed`, `kyb.status_changed`, `alert.created`, `alert.status_changed`,
`transaction.blocked`, `webhook.test`.

```csharp
using Protegey.Sdk;

string payload = rawRequestBody; // the RAW body — do not re-encode/re-serialize it
string timestamp = Request.Headers["X-Timestamp"]!;
string signature = Request.Headers["X-Signature"]!;

if (!WebhookVerifier.Verify(payload, timestamp, signature, yourWebhookSecret))
{
    return Results.Unauthorized();
}
```

`kyc.status_changed` fires on an automated verification result and on every manual status change (portal or
`UpdateSessionAsync`). Its `sessionId` is the same id `StartSessionAsync` returns, and it carries
`externalUserId`, `status`, `recommendation` and `decision` (with `decision.manualReview` after a
manual change, `previousStatus` / `newStatus` in UPPER_SNAKE_CASE), plus `kybClientId` and
`kybNameMatch` for a KYB-linked check. `kyb.status_changed` carries
`submissionStatus` in UPPER_SNAKE_CASE. `Kyc.GetSessionAsync` stays the polling fallback.

## `baseUrl` — no default, on purpose

Confirm the current value with Protegey before you ship — it can differ between environments and
change independently of this package's version.

## Development

```bash
dotnet test
# or, without a local .NET SDK:
docker run --rm -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test
```

## Security note

Keep your API key and webhook secret out of source control, the same way you would any other
secret.
