using System.Globalization;

namespace SwiftBets.History.Application;

/// <summary>A page of one punter's placed coupons, newest first; <see cref="Before"/> is the last row of the previous page.</summary>
public sealed record CouponFilter(string? Status, string? BetType, DateTimeOffset? From, DateTimeOffset? To, CouponCursor? Before, int Limit)
{
    public static IReadOnlyList<string> Statuses { get; } = ["open", "won", "lost", "void", "cashedOut"];

    public static IReadOnlyList<string> BetTypes { get; } = ["single", "accumulator", "system"];

    /// <summary>The reason the filter is refused, or null when every value is one the store understands.</summary>
    public string? Problem() =>
        Status is not null && !Statuses.Contains(Status) ? "status must be one of " + string.Join(", ", Statuses) + "."
        : BetType is not null && !BetTypes.Contains(BetType) ? "betType must be one of " + string.Join(", ", BetTypes) + "."
        : From is { } from && To is { } to && to <= from ? "to must be after from."
        : null;
}

/// <summary>A keyset position: placement time then coupon id, both descending. Its text form is opaque to clients.</summary>
public readonly record struct CouponCursor(DateTime PlacedAt, Guid CouponId)
{
    public override string ToString() => PlacedAt.Ticks.ToString(CultureInfo.InvariantCulture) + "_" + CouponId.ToString("N");

    public static bool TryParse(string? text, out CouponCursor cursor)
    {
        cursor = default;
        var parts = text?.Split('_');
        if (parts is not [var ticks, var id] || !long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var t)
            || t < DateTime.MinValue.Ticks || t > DateTime.MaxValue.Ticks || !Guid.TryParseExact(id, "N", out var couponId))
        {
            return false;
        }

        cursor = new CouponCursor(new DateTime(t, DateTimeKind.Utc), couponId);
        return true;
    }
}
