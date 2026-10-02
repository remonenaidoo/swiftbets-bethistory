namespace SwiftBets.History.Application;

/// <summary>A paid win as the public ticker shows it; the punter is only ever a masked account.</summary>
public sealed record RecentWin(Guid CouponId, string Account, string? BetType, long PaidMinorUnits, string Currency, DateTime PaidAt);

public static class AccountMask
{
    /// <summary>"****" and the last four hex characters of the punter id, so a win is attributable to no one.</summary>
    public static string For(Guid punterId) => "****" + punterId.ToString("N")[^4..];
}
