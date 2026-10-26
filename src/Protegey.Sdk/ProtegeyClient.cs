namespace Protegey.Sdk;

/// <summary>
/// The Protegey SDK for .NET backends — transaction reporting and identity verification
/// sessions, called directly from your server with your own API key, plus a webhook signature
/// verifier.
///
/// <code>
/// var protegey = new ProtegeyClient("YOUR_API_KEY", "https://api.protegey.com");
/// var result = await protegey.Transactions.ReportAsync(new Dictionary&lt;string, object?&gt;
/// {
///     ["externalTransactionId"] = "tx-1",
///     ["externalCustomerId"] = "cust-1",
///     ["direction"] = "DEBIT",
///     ["amount"] = 5000,
///     ["transactionType"] = "cashout",
/// });
/// var session = await protegey.Kyc.StartSessionAsync("cust-1");
/// </code>
///
/// <para>Named <c>ProtegeyClient</c>, not bare <c>Protegey</c> (unlike the Java/PHP SDKs' main
/// class) — a type sharing its name with the first segment of its own namespace
/// (<c>Protegey.Sdk</c>) is a real C# ambiguity, not just a style choice; every consumer would
/// hit a "namespace used like a type" compile error. Same pattern .NET SDKs usually land on
/// anyway (e.g. Azure's <c>BlobServiceClient</c>, Stripe's <c>StripeClient</c>).</para>
///
/// <para><c>baseUrl</c> has no default — confirm the current value with Protegey (it may differ
/// between environments and can change independently of this package's version).</para>
/// </summary>
public sealed class ProtegeyClient
{
    public TransactionsModule Transactions { get; }
    public KycModule Kyc { get; }

    public ProtegeyClient(string apiKey, string baseUrl) : this(apiKey, baseUrl, null)
    {
    }

    /// <param name="httpClient">
    /// Inject your own (e.g. one with custom timeouts, or a test double) — defaults to a plain
    /// <see cref="HttpClient"/> instance if omitted.
    /// </param>
    public ProtegeyClient(string apiKey, string baseUrl, HttpClient? httpClient)
    {
        var http = new Client(apiKey, baseUrl, httpClient);
        Transactions = new TransactionsModule(http);
        Kyc = new KycModule(http);
    }
}
