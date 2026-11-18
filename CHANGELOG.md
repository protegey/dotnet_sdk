## 0.3.0

Brings the SDK in line with the current partner API (all 23 `/partner-api` endpoints).

- New modules: `Alerts` (list, get, update, convert-to-case, flag-transaction), `Kyb`
  (list, get, invite, resend invitation, screen, decide), `DeviceEvents`, `BehavioralEvents`,
  `Sanctions`, `SharedSignal`.
- `Kyc`: added `ListSessionsAsync`, `UpdateSessionAsync`, `ResendSessionAsync`. The list `status`
  filter is converted to the Title Case form the API requires (`IN_REVIEW` → `In Review`).
- `Transactions`: added `ListAsync` and `GetAsync` (accepts the Protegey id or your external id).
- `Kyb.InviteApplicantAsync` takes `contactEmail` and/or `contactPhone` (at least one) and the
  response's `applicationUrl`; KYB responses have the internal `tokenHash` / `documentFilePath`
  fields stripped.
- Alert list `status`/`disposition` filters and the sanctions `type` are lowercased before sending,
  as the API requires.
- `ProtegeyApiException` now exposes `ErrorCode` (`PARTNER_SUSPENDED`, `PARTNER_CONTRACT_REQUIRED`)
  and the parsed error `Body`.
- `WebhookVerifier.Verify` returns false on a missing signature/timestamp and accepts an
  uppercase hex signature. New `WebhookEventTypes` constants for the payload `type` field.
- Docs: webhook retries are 5 attempts over ~3 h (not "one retry"); `kyc.status_changed` has a
  null `sessionId`; sandbox vs live is chosen only by the API key.

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
