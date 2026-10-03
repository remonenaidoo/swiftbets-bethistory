namespace SwiftBets.History.Application;

/// <summary>Timestamps are UTC; Npgsql materialises timestamptz as a UTC DateTime. BoostBonus is the accumulator boost paid, inside Payout.</summary>
public sealed record CouponHistoryRow(
    Guid CouponId,
    string Status,
    string? BetType,
    long? Stake,
    string Currency,
    decimal? TotalOdds,
    long? PotentialPayout,
    string? LegsJson,
    DateTime? PlacedAt,
    int SettlementVersion,
    long? Payout,
    long PaidToDate,
    DateTime UpdatedAt,
    Guid PunterId,
    decimal AccaBoostPercent = 0m,
    long? BoostBonus = null);
