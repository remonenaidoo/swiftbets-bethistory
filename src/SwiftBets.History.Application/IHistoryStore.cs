using SwiftBets.Contracts.Payout;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Settlement;

namespace SwiftBets.History.Application;

/// <summary>
/// The punter-facing read model. Each projection is an idempotent upsert guarded by version, so events may arrive
/// duplicated or out of order (a settlement before its placement) and the row still converges.
/// </summary>
public interface IHistoryStore
{
    Task ProjectPlacedAsync(CouponPlacedV1 placed, CancellationToken cancellationToken);

    /// <summary>Every coupon, system bets included; a coupon also seen as V1 is projected once.</summary>
    Task ProjectPlacedAsync(CouponPlacedV2 placed, CancellationToken cancellationToken);

    Task ProjectSettledAsync(CouponSettledV1 settled, CancellationToken cancellationToken);

    Task ProjectPaidAsync(PayoutCompletedV1 paid, CancellationToken cancellationToken);

    Task<IReadOnlyList<CouponHistoryRow>> ListAsync(Guid punterId, int limit, CancellationToken cancellationToken);

    Task<CouponHistoryRow?> GetAsync(Guid couponId, CancellationToken cancellationToken);
}
