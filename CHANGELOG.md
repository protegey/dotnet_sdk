## 0.2.0

- `Protegey.Kyc.StartSessionAsync()` now accepts optional `email` and `phone` — fed into
  identity-resolution's cross-partner matching; omitted from the request body when not
  provided. Existing single-argument calls are unaffected.
- Fixed stale lowercase `decision` fixtures in `TransactionsModuleTests` — the real wire value
  is always `'CLEAR'` / `'REVIEW'` / `'BLOCKED'`, never lowercase.
- Fixed a stale mixed-case `status` fixture in `KycModuleTests` left behind by the Nov 14
  UPPER_SNAKE_CASE rollout — the fake-response fixture still said `"Approved"` while the
  assertion already expected `"APPROVED"`.

## 0.1.0

- Initial release.
- `Protegey.Transactions.ReportAsync()` — transaction monitoring.
- `Protegey.Kyc.StartSessionAsync()` / `Protegey.Kyc.GetSessionAsync()` — identity verification + webhook-polling fallback.
- `WebhookVerifier.Verify()` — HMAC-SHA256 signature verification for incoming webhooks.
- `baseUrl` is a required constructor parameter, with no built-in default — see the README for why.
