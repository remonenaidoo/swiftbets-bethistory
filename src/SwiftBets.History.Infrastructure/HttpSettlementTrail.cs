using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.History.Application;
using SwiftBets.History.Infrastructure.Integrity;

namespace SwiftBets.History.Infrastructure;

/// <summary>Reads settlement's coupon state with this service's token. A failure leaves the detail without results rather than failing it.</summary>
public sealed partial class HttpSettlementTrail(IHttpClientFactory factory, ClientCredentialsTokenProvider tokens, ILogger<HttpSettlementTrail> logger) : ISettlementTrail
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SettlementTrail?> GetAsync(Guid couponId, CancellationToken cancellationToken)
    {
        try
        {
            var http = factory.CreateClient(HttpIntegritySources.Settlement);
            for (var attempt = 0; ; attempt++)
            {
                var token = await tokens.GetTokenAsync(cancellationToken);
                using var message = new HttpRequestMessage(HttpMethod.Get, $"coupons/{couponId}/state");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await http.SendAsync(message, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    tokens.Invalidate(token);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var state = await response.Content.ReadFromJsonAsync<StateView>(Json, cancellationToken);
                return state is null ? null : new SettlementTrail(
                    [.. state.Legs.Select(l => new LegResult(l.LegId, l.Outcome is { Length: > 0 } o ? JsonNamingPolicy.CamelCase.ConvertName(o) : null))],
                    [.. state.Settlements.OrderBy(s => s.Version).Select(s => new SettlementEntry(s.Version, JsonNamingPolicy.CamelCase.ConvertName(s.Outcome), s.Payout, s.SettledAt))]);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, couponId, ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Settlement state for coupon {CouponId} is unavailable; the detail is shown without results.")]
    private static partial void LogUnavailable(ILogger logger, Guid couponId, Exception exception);

    private sealed record StateView(IReadOnlyList<LegView> Legs, IReadOnlyList<SettlementView> Settlements);

    private sealed record LegView(Guid LegId, string? Outcome);

    private sealed record SettlementView(int Version, string Outcome, long Payout, DateTimeOffset SettledAt);
}
