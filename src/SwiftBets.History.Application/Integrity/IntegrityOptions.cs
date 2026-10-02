using System.ComponentModel.DataAnnotations;

namespace SwiftBets.History.Application.Integrity;

public sealed class IntegrityOptions
{
    public const string SectionName = "Integrity";

    /// <summary>Off unless the owning services' addresses and a service identity are configured.</summary>
    public bool Enabled { get; set; }

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>How far back each run looks at placed coupons.</summary>
    [Range(1, 24)]
    public int WindowHours { get; set; } = 24;

    /// <summary>Events younger than this are in flight, not missing.</summary>
    [Range(1, 120)]
    public int GraceMinutes { get; set; } = 10;
}
