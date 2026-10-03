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
    /// <summary>Every coupon, system bets included.</summary>
    Task ProjectPlacedAsync(CouponPlacedV2 placed, CancellationToken cancellationToken);

    Task ProjectSettledAsync(CouponSettledV2 settled, CancellationToken cancellationToken);

    Task ProjectPaidAsync(PayoutCompletedV1 paid, CancellationToken cancellationToken);

    Task<IReadOnlyList<CouponHistoryRow>> ListAsync(Guid punterId, int limit, CancellationToken cancellationToken);

    /// <summary>One page of a punter's placed coupons matching <paramref name="filter"/>, newest first.</summary>
    Task<IReadOnlyList<CouponHistoryRow>> SearchAsync(Guid punterId, CouponFilter filter, CancellationToken cancellationToken);

    /// <summary>Only coupons not yet settled, newest first.</summary>
    Task<IReadOnlyList<CouponHistoryRow>> ListOpenAsync(Guid punterId, int limit, CancellationToken cancellationToken);

    /// <summary>Coupons open longer than <paramref name="staleHours"/>, and settlements whose placement never arrived after <paramref name="graceMinutes"/>.</summary>
    Task<IReadOnlyList<IntegrityFinding>> FindIntegrityProblemsAsync(int staleHours, int graceMinutes, CancellationToken cancellationToken);

    Task<CouponHistoryRow?> GetAsync(Guid couponId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CouponHistoryRow>> GetManyAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken);

    /// <summary>The most recent paid wins and cash-outs, newest first.</summary>
    Task<IReadOnlyList<RecentWin>> RecentWinsAsync(int limit, CancellationToken cancellationToken);

    Task RecordIntegrityRunAsync(Integrity.IntegrityRun run, CancellationToken cancellationToken);

    Task<Integrity.IntegrityRun?> GetLatestIntegrityRunAsync(CancellationToken cancellationToken);
}
