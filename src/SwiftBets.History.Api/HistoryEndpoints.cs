using System.Text.Json;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Serialization;
using SwiftBets.History.Application;

namespace SwiftBets.History.Api;

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Filters and a keyset cursor: pass the last item's cursor as before for the next page.
        endpoints.MapGet("/me/coupons", (HttpContext context, IHistoryStore history, int? limit, bool? open, string? status, string? betType,
                DateTimeOffset? from, DateTimeOffset? to, string? before, CancellationToken cancellationToken) =>
            Search(context, history, PunterId(context), limit ?? 25, open == true ? "open" : status, betType, from, to, before, cancellationToken))
        .RequireAuthorization(Roles.Punter);

        endpoints.MapGet("/me/coupons/{couponId:guid}", async (HttpContext context, Guid couponId, IHistoryStore history, ISettlementTrail trail, CancellationToken cancellationToken) =>
            await history.GetAsync(couponId, cancellationToken) is { } row && row.PunterId == PunterId(context)
                ? Results.Json(await CouponDetail.ForAsync(row, trail, cancellationToken), ContractJson.Options)
                : Error.NotFound("coupon_not_found", "No such coupon.").ToHttpResult(context))
        .RequireAuthorization(Roles.Punter);

        // Public for the site's ticker: masked accounts only, and cacheable for a few seconds.
        endpoints.MapGet("/recent-wins", async (HttpContext context, IHistoryStore history, int? limit, CancellationToken cancellationToken) =>
        {
            var wins = await history.RecentWinsAsync(Math.Clamp(limit ?? 12, 1, 30), cancellationToken);
            context.Response.Headers.CacheControl = "public, max-age=5";
            return Results.Json(wins.Select(w => new { w.CouponId, w.Account, w.BetType, Payout = new { MinorUnits = w.PaidMinorUnits, w.Currency }, w.PaidAt }), ContractJson.Options);
        })
        .AllowAnonymous();

        var admin = endpoints.MapGroup("/admin/history").RequireAuthorization(Roles.Operator);
        admin.MapGet("/punters/{punterId:guid}/coupons", (HttpContext context, Guid punterId, IHistoryStore history, int? limit, bool? open, string? status, string? betType,
                DateTimeOffset? from, DateTimeOffset? to, string? before, CancellationToken cancellationToken) =>
            Search(context, history, punterId, limit ?? 50, open == true ? "open" : status, betType, from, to, before, cancellationToken))
        .RequireAuthorization(BetsRead);
        admin.MapGet("/integrity", async (IHistoryStore history, int? staleHours, int? graceMinutes, CancellationToken cancellationToken) =>
            Results.Json(await history.FindIntegrityProblemsAsync(staleHours ?? 72, graceMinutes ?? 30, cancellationToken), ContractJson.Options));
        admin.MapGet("/integrity/cross-store", async (HttpContext context, IHistoryStore history, CancellationToken cancellationToken) =>
            await history.GetLatestIntegrityRunAsync(cancellationToken) is { } run
                ? Results.Json(run, ContractJson.Options)
                : Error.NotFound("no_integrity_run", "No cross-store integrity run has completed yet.").ToHttpResult(context));
        admin.MapGet("/coupons/{couponId:guid}", async (HttpContext context, Guid couponId, IHistoryStore history, ISettlementTrail trail, CancellationToken cancellationToken) =>
            await history.GetAsync(couponId, cancellationToken) is { } row
                ? Results.Json(await CouponDetail.ForAsync(row, trail, cancellationToken), ContractJson.Options)
                : Error.NotFound("coupon_not_found", "No such coupon.").ToHttpResult(context))
        .RequireAuthorization(BetsRead);

        return endpoints;
    }

    /// <summary>Staff permission to read any customer's bets (D153); the console hides the screen without it.</summary>
    public const string BetsRead = "bets.read";

    private static async Task<IResult> Search(HttpContext context, IHistoryStore history, Guid punterId, int limit, string? status, string? betType,
        DateTimeOffset? from, DateTimeOffset? to, string? before, CancellationToken cancellationToken)
    {
        CouponCursor? cursor = null;
        if (before is { Length: > 0 })
        {
            if (!CouponCursor.TryParse(before, out var parsed))
            {
                return Error.Validation("invalid_cursor", "before is not a cursor this service issued.").ToHttpResult(context);
            }

            cursor = parsed;
        }

        var filter = new CouponFilter(status, betType, from, to, cursor, limit);
        if (filter.Problem() is { } problem)
        {
            return Error.Validation("invalid_filter", problem).ToHttpResult(context);
        }

        return Results.Json((await history.SearchAsync(punterId, filter, cancellationToken)).Select(HistoryCoupon.From), ContractJson.Options);
    }

    private static Guid PunterId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) ? id : throw new BadHttpRequestException("Token subject is not a user id.", 401);

    /// <summary>A history row with the legs as JSON rather than the stored string.</summary>
    private sealed record HistoryCoupon(
        Guid CouponId, Guid PunterId, string Status, string? BetType, long? Stake, string Currency, decimal? TotalOdds, long? PotentialPayout,
        JsonElement? Legs, DateTime? PlacedAt, int SettlementVersion, long? Payout, long PaidToDate, DateTime UpdatedAt, string? Cursor)
    {
        public static HistoryCoupon From(CouponHistoryRow row) => new(
            row.CouponId, row.PunterId, row.Status, row.BetType, row.Stake, row.Currency, row.TotalOdds, row.PotentialPayout,
            row.LegsJson is { Length: > 0 } legs ? JsonDocument.Parse(legs).RootElement.Clone() : null,
            row.PlacedAt, row.SettlementVersion, row.Payout, row.PaidToDate, row.UpdatedAt,
            row.PlacedAt is { } at ? new CouponCursor(DateTime.SpecifyKind(at, DateTimeKind.Utc), row.CouponId).ToString() : null);
    }

    /// <summary>A leg as placed, with its result once settlement has one.</summary>
    private sealed record DetailLeg(Guid LegId, string FixtureId, string MarketId, string SelectionId, decimal Odds, bool IsBanker, string? Result);

    /// <summary>What the punter took when they cashed out; null on a coupon that never was.</summary>
    private sealed record CashoutInfo(long Amount, DateTimeOffset? CashedOutAt);

    /// <summary>One coupon with leg results, every settlement in version order and any cashout.</summary>
    private sealed record CouponDetail(
        Guid CouponId, Guid PunterId, string Status, string? BetType, long? Stake, string Currency, decimal? TotalOdds, long? PotentialPayout,
        IReadOnlyList<DetailLeg> Legs, DateTime? PlacedAt, int SettlementVersion, long? Payout, long PaidToDate, DateTime UpdatedAt,
        IReadOnlyList<SettlementEntry> Settlements, CashoutInfo? Cashout, bool ResultsAvailable)
    {
        public static async Task<CouponDetail> ForAsync(CouponHistoryRow row, ISettlementTrail trail, CancellationToken cancellationToken)
        {
            var settled = await trail.GetAsync(row.CouponId, cancellationToken);
            var results = settled?.Legs.ToDictionary(l => l.LegId, l => l.Outcome) ?? [];
            var placed = row.LegsJson is { Length: > 0 } json ? JsonSerializer.Deserialize<List<CouponLegV2>>(json, ContractJson.Options) ?? [] : [];
            var settlements = settled?.Settlements ?? [];
            var cashedOut = settlements.LastOrDefault(s => s.Outcome == "cashedOut");
            var cashout = cashedOut is not null ? new CashoutInfo(cashedOut.Payout, cashedOut.SettledAt)
                : row.Status == "cashedOut" ? new CashoutInfo(row.Payout ?? 0, null)
                : null;
            return new CouponDetail(
                row.CouponId, row.PunterId, row.Status, row.BetType, row.Stake, row.Currency, row.TotalOdds, row.PotentialPayout,
                [.. placed.Select(l => new DetailLeg(l.LegId, l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.IsBanker, results.GetValueOrDefault(l.LegId)))],
                row.PlacedAt, row.SettlementVersion, row.Payout, row.PaidToDate, row.UpdatedAt, settlements, cashout, settled is not null);
        }
    }
}
