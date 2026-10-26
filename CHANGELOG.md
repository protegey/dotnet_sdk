## 0.1.0

- Initial release.
- `Protegey.Transactions.ReportAsync()` — transaction monitoring.
- `Protegey.Kyc.StartSessionAsync()` / `Protegey.Kyc.GetSessionAsync()` — identity verification + webhook-polling fallback.
- `WebhookVerifier.Verify()` — HMAC-SHA256 signature verification for incoming webhooks.
- `baseUrl` is a required constructor parameter, with no built-in default — see the README for why.
