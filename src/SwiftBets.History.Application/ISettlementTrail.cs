namespace SwiftBets.History.Application;

/// <summary>How one leg fell, camelCase (won, lost, void); null while its result is not in.</summary>
public sealed record LegResult(Guid LegId, string? Outcome);

/// <summary>One settlement of a coupon. A resettlement adds a version; the latest is the one that stands.</summary>
public sealed record SettlementEntry(int Version, string Outcome, long Payout, DateTimeOffset SettledAt);

public sealed record SettlementTrail(IReadOnlyList<LegResult> Legs, IReadOnlyList<SettlementEntry> Settlements);

/// <summary>Settlement's own record of a coupon's leg results and settlements; null when it cannot be read right now.</summary>
public interface ISettlementTrail
{
    Task<SettlementTrail?> GetAsync(Guid couponId, CancellationToken cancellationToken);
}

/// <summary>Where settlement is not configured, a detail shows the coupon without results.</summary>
public sealed class NoSettlementTrail : ISettlementTrail
{
    public Task<SettlementTrail?> GetAsync(Guid couponId, CancellationToken cancellationToken) => Task.FromResult<SettlementTrail?>(null);
}
