# Protegey.Sdk (.NET)

Official Protegey SDK for .NET backends — transaction reporting, identity verification sessions,
and webhook signature verification, called directly from your server with your own API key.

Backend-only: no device fingerprinting or behavioral biometrics here (those are browser/mobile
concepts) — see `@protegey/sdk` (JS), `protegey_sdk` (Flutter) or `@protegey/react-native-sdk` for
those.

Targets .NET 8+. Zero external dependencies — built entirely on `System.Net.Http.HttpClient` and
`System.Text.Json`, both already in the base class library.

## Install

Not yet published to NuGet.org — install directly from GitHub for now:

```bash
git clone https://github.com/protegey/protegey_dotnet_sdk.git
cd protegey_dotnet_sdk
dotnet pack src/Protegey.Sdk -c Release
# then reference the produced .nupkg from src/Protegey.Sdk/bin/Release/ in your own project,
# e.g. via a local NuGet feed, or just add a ProjectReference to src/Protegey.Sdk if you're
# vendoring the source directly.
```

Source: [github.com/protegey/protegey_dotnet_sdk](https://github.com/protegey/protegey_dotnet_sdk)

## Usage

```csharp
using Protegey.Sdk;

var protegey = new ProtegeyClient("YOUR_API_KEY", "https://api.protegey.com");

// Transactions
var result = await protegey.Transactions.ReportAsync(new Dictionary<string, object?>
{
    ["externalTransactionId"] = "tx-00234",
    ["externalCustomerId"] = "cust-9981",
    ["customerName"] = "Awa Diallo", // self-declared, shows up directly in the Pan Studio transactions table
    ["direction"] = "DEBIT",
    ["amount"] = 250000,
    ["currency"] = "XOF",
    ["transactionType"] = "cashout",
    ["isCash"] = true,
});

// Bank/telco: channel, counterpartyInstitutionCode and counterpartyCountry let Pan Studio rules
// target bank-wire and cross-border scenarios, not just mobile-money structuring.
await protegey.Transactions.ReportAsync(new Dictionary<string, object?>
{
    ["externalTransactionId"] = "tx-00235",
    ["externalCustomerId"] = "cust-9981",
    ["direction"] = "DEBIT",
    ["amount"] = 2000000,
    ["transactionType"] = "wire",
    ["channel"] = "online",
    ["counterpartyInstitutionCode"] = "ECOBKTGTG",
    ["counterpartyCountry"] = "SN",
});

// Identity verification — no manual API call needed, the SDK starts the session and hands back the link
var session = await protegey.Kyc.StartSessionAsync("cust-9981");
// Send session["url"] to your user however you like (SMS, email, your own hosted redirect page)

// Polling fallback — webhook delivery is best-effort (one retry, no queue), so use this if you're
// not sure a delivery ever arrived, or just want to double-check a session's status.
var current = await protegey.Kyc.GetSessionAsync((string)session["sessionId"]!);
```

## Verifying incoming webhooks

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

## `baseUrl` — no default, on purpose

Confirm the current value with Protegey before you ship — it can differ between environments and
change independently of this package's version.

## Development

```bash
dotnet test
```

## Security note

Keep your API key and webhook secret out of source control, the same way you would any other
secret.
