using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.History.Application.Integrity;

namespace SwiftBets.History.Infrastructure.Integrity;

/// <summary>Each owning service's internal integrity digest, called with this service's client-credentials token.</summary>
public sealed class HttpIntegritySources(IHttpClientFactory factory, ClientCredentialsTokenProvider tokens) : IIntegritySources
{
    public const string Placement = "integrity-placement";
    public const string Settlement = "integrity-settlement";
    public const string Payout = "integrity-payout";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<PlacedFact>> PlacedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        SendAsync<PlacedFact>(Placement, () => new HttpRequestMessage(HttpMethod.Get,
            $"internal/integrity/coupons?from={Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))}&to={Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture))}"), cancellationToken);

    public Task<IReadOnlyList<SettledFact>> SettledAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken) =>
        SendAsync<SettledFact>(Settlement, () => new HttpRequestMessage(HttpMethod.Post, "internal/integrity/settlements") { Content = JsonContent.Create(new { couponIds }) }, cancellationToken);

    public Task<IReadOnlyList<PaidFact>> PaidAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken) =>
        SendAsync<PaidFact>(Payout, () => new HttpRequestMessage(HttpMethod.Post, "internal/integrity/payouts") { Content = JsonContent.Create(new { couponIds }) }, cancellationToken);

    private async Task<IReadOnlyList<T>> SendAsync<T>(string client, Func<HttpRequestMessage> request, CancellationToken cancellationToken)
    {
        var http = factory.CreateClient(client);
        for (var attempt = 0; ; attempt++)
        {
            var token = await tokens.GetTokenAsync(cancellationToken);
            using var message = request();
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                tokens.Invalidate(token);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<T>>(Json, cancellationToken) ?? [];
        }
    }
}
