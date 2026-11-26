namespace Protegey.Sdk;

/// <summary>
/// Business (KYB) applicants — <c>/partner-api/kyb/applicants</c>. KYB has no sandbox/live split:
/// both keys act on the same applicants.
///
/// <para>Wire values: applicant <c>status</c> — <c>INVITED</c>, <c>PENDING_REVIEW</c>,
/// <c>MORE_INFO_REQUIRED</c>, <c>ACTIVE</c>, <c>REJECTED</c>; <c>submission.status</c> —
/// <c>PENDING</c>, <c>SUBMITTED</c>, <c>MORE_INFO_REQUIRED</c>, <c>APPROVED</c>,
/// <c>REJECTED</c>.</para>
///
/// <para>The API currently also returns two internal submission fields, <c>tokenHash</c> and
/// <c>documentFilePath</c>. They are not part of the contract, so the SDK strips them from every
/// response — don't rely on them.</para>
/// </summary>
public sealed class KybModule
{
    private static readonly string[] InternalFields = { "tokenHash", "documentFilePath" };

    private readonly Client _http;

    internal KybModule(Client http)
    {
        _http = http;
    }

    /// <summary>
    /// <c>GET /partner-api/kyb/applicants</c> — paginated. Optional query keys: <c>page</c>,
    /// <c>limit</c>, <c>search</c>, <c>status</c> (any case), <c>stage</c>
    /// (<c>"invited"</c>|<c>"responded"</c>). Each row carries its <c>submission</c> (or null).
    /// </summary>
    public async Task<Dictionary<string, object?>> ListApplicantsAsync(Dictionary<string, object?>? query = null, CancellationToken cancellationToken = default)
    {
        var result = await _http.GetAsync("/partner-api/kyb/applicants", query, cancellationToken).ConfigureAwait(false);
        if (result.GetValueOrDefault("data") is List<object?> rows)
        {
            foreach (var row in rows.OfType<Dictionary<string, object?>>())
            {
                StripInternal(row);
            }
        }
        return result;
    }

    /// <summary>
    /// <c>GET /partner-api/kyb/applicants/{clientId}</c> — the applicant with its <c>submission</c>
    /// and <c>kycVerifications</c> (the identity checks requested for it, newest first — same items
    /// as <see cref="ListKycVerificationsAsync"/>).
    /// </summary>
    public async Task<Dictionary<string, object?>> GetApplicantAsync(string clientId, CancellationToken cancellationToken = default)
        => StripInternal(await _http.GetAsync("/partner-api/kyb/applicants/" + Client.Segment(clientId), null, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// <c>POST /partner-api/kyb/applicants</c> — invites a business to fill in your KYB
    /// questionnaire. At least one of <paramref name="contactEmail"/> or
    /// <paramref name="contactPhone"/> is required (the API answers 400 otherwise); an invitation
    /// email is only sent when an email is given. <paramref name="templateId"/> picks a form
    /// template (omit for your default).
    ///
    /// <para>Returns the applicant (without <c>submission</c>) plus <c>applicationUrl</c> — the
    /// link to send the business yourself, e.g. by SMS when you only have a phone number.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> InviteApplicantAsync(string contactName, string? contactEmail = null, string? contactPhone = null, string? templateId = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["contactName"] = contactName };
        if (contactEmail is not null)
        {
            body["contactEmail"] = contactEmail;
        }
        if (contactPhone is not null)
        {
            body["contactPhone"] = contactPhone;
        }
        if (templateId is not null)
        {
            body["templateId"] = templateId;
        }
        return _http.PostAsync("/partner-api/kyb/applicants", body, cancellationToken);
    }

    /// <summary>
    /// <c>POST /partner-api/kyb/applicants/{clientId}/resend-invitation</c> — rotates the link.
    /// Only allowed while the submission is <c>PENDING</c> or <c>MORE_INFO_REQUIRED</c>. Returns
    /// the <em>submission</em> object (not the applicant) plus the new <c>applicationUrl</c>.
    /// </summary>
    public async Task<Dictionary<string, object?>> ResendInvitationAsync(string clientId, CancellationToken cancellationToken = default)
        => StripInternal(await _http.PostAsync("/partner-api/kyb/applicants/" + Client.Segment(clientId) + "/resend-invitation", null, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// <c>GET /partner-api/kyb/applicants/{clientId}/screen</c> — runs sanctions/PEP screening of
    /// the business and each declared owner and stores the result on the submission. Returns
    /// <c>{business, owners[], screenedAt, provider, providers, defaultListChecked, degraded}</c>;
    /// each <c>decision</c> is <c>BLOCKED</c>|<c>REVIEW</c>|<c>CLEAR</c>.
    /// </summary>
    public Task<Dictionary<string, object?>> ScreenApplicantAsync(string clientId, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/kyb/applicants/" + Client.Segment(clientId) + "/screen", null, cancellationToken);

    /// <summary>
    /// <c>PATCH /partner-api/kyb/applicants/{clientId}/decide</c> — review a <c>SUBMITTED</c>
    /// application. <paramref name="decision"/>: <c>"APPROVE"</c>|<c>"REJECT"</c>|
    /// <c>"REQUEST_MORE_INFO"</c>. <paramref name="reason"/> (min 5 chars) is required for
    /// REJECT, <paramref name="note"/> (min 5 chars) for REQUEST_MORE_INFO.
    ///
    /// <para>Returns the applicant (without <c>submission</c>); <c>applicationUrl</c> is present
    /// only for REQUEST_MORE_INFO. Fires <c>kyb.status_changed</c>.</para>
    /// </summary>
    public Task<Dictionary<string, object?>> DecideAsync(string clientId, string decision, string? reason = null, string? note = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["decision"] = decision };
        if (reason is not null)
        {
            body["reason"] = reason;
        }
        if (note is not null)
        {
            body["note"] = note;
        }
        return _http.PatchAsync("/partner-api/kyb/applicants/" + Client.Segment(clientId) + "/decide", body, cancellationToken);
    }

    /// <summary>
    /// <c>POST /partner-api/kyb/applicants/{clientId}/kyc-sessions</c> — asks the person behind an
    /// applicant to verify their identity (a standard KYC session linked to the KYB, billed as one).
    /// Callable any number of times; each call is a new session. Only once the applicant has
    /// submitted its application (400 otherwise; 404 for an unknown applicant).
    ///
    /// <para>All optional: <paramref name="fullName"/> (defaults to the applicant contact name) is
    /// compared with the name on the ID document — the result is the session's
    /// <c>kybNameMatch</c>; <paramref name="email"/> — Protegey emails the link only when it is
    /// given, otherwise relay <c>url</c> yourself; <paramref name="phone"/> (defaults to the
    /// contact phone); <paramref name="externalUserId"/>, echoed on the session and its
    /// webhooks.</para>
    ///
    /// <para>Returns <c>{sessionId, kybClientId, url, expiresAt, emailedTo}</c> — <c>url</c> is
    /// single-use and valid until <c>expiresAt</c> (your capture-link lifetime setting, never less
    /// than 7 days here); <c>emailedTo</c> is null when no email was given. Results arrive on
    /// <c>kyc.status_changed</c> (with <c>kybClientId</c>).</para>
    /// </summary>
    public Task<Dictionary<string, object?>> RequestKycVerificationAsync(string clientId, string? fullName = null, string? email = null, string? phone = null, string? externalUserId = null, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>();
        if (fullName is not null)
        {
            body["fullName"] = fullName;
        }
        if (email is not null)
        {
            body["email"] = email;
        }
        if (phone is not null)
        {
            body["phone"] = phone;
        }
        if (externalUserId is not null)
        {
            body["externalUserId"] = externalUserId;
        }
        return _http.PostAsync("/partner-api/kyb/applicants/" + Client.Segment(clientId) + "/kyc-sessions", body, cancellationToken);
    }

    /// <summary>
    /// <c>GET /partner-api/kyb/applicants/{clientId}/kyc-sessions</c> — <c>{data}</c>, the identity
    /// checks requested for the applicant, newest first. Each item: <c>sessionId</c>,
    /// <c>status</c>, <c>outcome</c>, <c>recommendation</c>, <c>expectedName</c>,
    /// <c>documentFullName</c>, <c>nameMatch</c> (<c>MATCH</c>|<c>MISMATCH</c>|null),
    /// <c>livenessScore</c>, <c>faceMatchScore</c>, <c>livemode</c>, <c>createdAt</c>,
    /// <c>updatedAt</c>. Use <see cref="KycModule.GetSessionAsync"/> for images and full detail.
    /// </summary>
    public Task<Dictionary<string, object?>> ListKycVerificationsAsync(string clientId, CancellationToken cancellationToken = default)
        => _http.GetAsync("/partner-api/kyb/applicants/" + Client.Segment(clientId) + "/kyc-sessions", null, cancellationToken);

    private static Dictionary<string, object?> StripInternal(Dictionary<string, object?> obj)
    {
        foreach (var field in InternalFields)
        {
            obj.Remove(field);
        }
        if (obj.GetValueOrDefault("submission") is Dictionary<string, object?> submission)
        {
            StripInternal(submission);
        }
        return obj;
    }
}
