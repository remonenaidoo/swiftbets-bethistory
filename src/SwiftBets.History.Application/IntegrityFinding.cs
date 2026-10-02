namespace SwiftBets.History.Application;

/// <summary><c>staleOpen</c> or <c>settledWithoutPlacement</c>; <c>Since</c> is the row's last update (UTC).</summary>
public sealed record IntegrityFinding(Guid CouponId, string Problem, DateTime Since);
