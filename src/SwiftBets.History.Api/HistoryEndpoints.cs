using System.Text.Json;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Serialization;
using SwiftBets.History.Application;

namespace SwiftBets.History.Api;

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me/coupons", async (HttpContext context, IHistoryStore history, int? limit, CancellationToken cancellationToken) =>
            Results.Json((await history.ListAsync(PunterId(context), limit ?? 25, cancellationToken)).Select(HistoryCoupon.From), ContractJson.Options))
        .RequireAuthorization(Roles.Punter);

        endpoints.MapGet("/me/coupons/{couponId:guid}", async (HttpContext context, Guid couponId, IHistoryStore history, CancellationToken cancellationToken) =>
            await history.GetAsync(couponId, cancellationToken) is { } row && row.PunterId == PunterId(context)
                ? Results.Json(HistoryCoupon.From(row), ContractJson.Options)
                : Error.NotFound("coupon_not_found", "No such coupon.").ToHttpResult(context))
        .RequireAuthorization(Roles.Punter);

        var admin = endpoints.MapGroup("/admin/history").RequireAuthorization(Roles.Operator);
        admin.MapGet("/punters/{punterId:guid}/coupons", async (Guid punterId, IHistoryStore history, int? limit, CancellationToken cancellationToken) =>
            Results.Json((await history.ListAsync(punterId, limit ?? 50, cancellationToken)).Select(HistoryCoupon.From), ContractJson.Options));
        admin.MapGet("/coupons/{couponId:guid}", async (HttpContext context, Guid couponId, IHistoryStore history, CancellationToken cancellationToken) =>
            await history.GetAsync(couponId, cancellationToken) is { } row
                ? Results.Json(HistoryCoupon.From(row), ContractJson.Options)
                : Error.NotFound("coupon_not_found", "No such coupon.").ToHttpResult(context));

        return endpoints;
    }

    private static Guid PunterId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) ? id : throw new BadHttpRequestException("Token subject is not a user id.", 401);

    /// <summary>A history row with the legs as JSON rather than the stored string.</summary>
    private sealed record HistoryCoupon(
        Guid CouponId, Guid PunterId, string Status, string? BetType, long? Stake, string Currency, decimal? TotalOdds, long? PotentialPayout,
        JsonElement? Legs, DateTime? PlacedAt, int SettlementVersion, long? Payout, long PaidToDate, DateTime UpdatedAt)
    {
        public static HistoryCoupon From(CouponHistoryRow row) => new(
            row.CouponId, row.PunterId, row.Status, row.BetType, row.Stake, row.Currency, row.TotalOdds, row.PotentialPayout,
            row.LegsJson is { Length: > 0 } legs ? JsonDocument.Parse(legs).RootElement.Clone() : null,
            row.PlacedAt, row.SettlementVersion, row.Payout, row.PaidToDate, row.UpdatedAt);
    }
}
