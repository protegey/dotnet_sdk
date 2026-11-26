## 0.6.0

- `Kyb`: new `RequestKycVerificationAsync` (`POST /partner-api/kyb/applicants/{clientId}/kyc-sessions`
  — optional `fullName`, `email`, `phone`, `externalUserId`; returns
  `{sessionId, kybClientId, url, expiresAt, emailedTo}`, the link is emailed only when `email` is
  given) and `ListKycVerificationsAsync` (`GET …/kyc-sessions`, `{data}` with `nameMatch`
  `MATCH`|`MISMATCH`|null). `GetApplicantAsync` now documents `kycVerifications`. 400 until the
  applicant has submitted its application.
- `Kyc`: capture links are no longer fixed at 30 minutes — `StartSessionAsync` and
  `ResendSessionAsync` return `expiresAt`; the lifetime is a per-account KYC setting (30 min, 1 h,
  24 h, 3 days or 7 days, 24 h by default), at least 7 days for a KYB-linked check. Sessions carry
  `kybClientId` (list, detail, PATCH response); the detail adds `kybNameMatch` and
  `decision.documentFullName`. `kyc.status_changed` carries `kybClientId` and `kybNameMatch`.
- `Transactions` / `DeviceEvents`: optional GPS `location`
  (`{latitude, longitude, accuracyMeters?, capturedAt?, isMocked?}`). Transaction responses
  (`ReportAsync`, `GetAsync`) return `location` (rounded to ~100 m, with `gpsIpDistanceKm`,
  `impliedTravelSpeedKmh`, `isNewGpsArea`) or null.

## 0.5.0

- New `Liveness` module — 3D liveness, a separate paid product (KYC is unaffected):
  `StartSessionAsync` (selfie-only liveness check), `StartReverificationAsync` (liveness, then a
  face match against the customer's KYC face), `GetSessionAsync` and `ListSessionsAsync`. Each
  start returns a hosted single-use `url`, valid 30 minutes.
- A reverification of someone with no KYC face on record throws a 400 `ProtegeyApiException`
  whose `ErrorCode` is `LivenessErrorCodes.No3DEnrollment` (`NO_3D_ENROLLMENT`).

## 0.4.0

Aligned with the partner API (2026-11-19).

- New `CashDeposits` module: `RecordAsync` (`POST /partner-api/cash-deposits`), `ListAsync`
  (filters `page`, `limit` ≤ 100, `dateFrom`, `dateTo`, `externalCustomerId`, `minAmount`,
  `maxAmount`, `currency`, `channel`, `decision`, `branchId`, `agentId`) and `GetAsync` (Protegey id
  or `externalTransactionId`). A reused `externalTransactionId` answers 409; `amount` comes back as
  a decimal string.
- `Kyc`: documented `recommendation` (`APPROVE`|`REVIEW`|`REJECT`|null) and the `images` block
  (`documentFront`, `documentBack`, `selfie`, `documentFaceCrop`, each `{url, expiresAt}` or null,
  pre-signed for 1 hour and re-issued on every GET) on the list and detail responses, the new
  `externalUserId` exact-match list filter, and `recommendation` + `decision.manualReview` on the
  `UpdateSessionAsync` response.
- Docs fix: `kyc.status_changed` is also sent on manual status changes, its `sessionId` is the
  session id (it was wrongly documented as always null), and it carries `recommendation`.
  `kyb.status_changed.submissionStatus` and `manualReview.previousStatus/newStatus` are
  UPPER_SNAKE_CASE. Retries: 5 attempts over about 3 h, deduplicate on `eventId`.

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
