namespace SwiftBets.History.Application.Integrity;

public sealed record PlacedFact(Guid CouponId, long Stake, long PotentialPayout, string Currency, DateTimeOffset PlacedAt);

public sealed record SettledFact(Guid CouponId, int Version, string Outcome, long Payout, DateTimeOffset SettledAt);

public sealed record PaidFact(Guid CouponId, long PaidToDate, int LastVersion);

/// <summary>What each owning service says about a coupon; history is checked against these, never the other way round.</summary>
public interface IIntegritySources
{
    Task<IReadOnlyList<PlacedFact>> PlacedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettledFact>> SettledAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaidFact>> PaidAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken);
}

public sealed record CrossStoreFinding(Guid CouponId, string Problem, string Detail);

public sealed record IntegrityRun(Guid RunId, DateTimeOffset StartedAt, DateTimeOffset WindowFrom, DateTimeOffset WindowTo, int CouponsChecked, IReadOnlyList<CrossStoreFinding> Findings);
