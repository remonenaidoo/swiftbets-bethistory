namespace SwiftBets.History.Application;

/// <summary>Timestamps are UTC; Npgsql materialises timestamptz as a UTC DateTime.</summary>
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
    Guid PunterId);
