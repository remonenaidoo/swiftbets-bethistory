namespace SwiftBets.History.Application.Integrity;

/// <summary>
/// Compares history rows with what placement, settlement and payout hold for the same coupons. A settlement newer than
/// the grace period that history has not caught up with is a finding; one still inside it is assumed to be in flight.
/// </summary>
public static class IntegrityCheck
{
    public static IReadOnlyList<CrossStoreFinding> Compare(
        IReadOnlyList<PlacedFact> placed, IReadOnlyDictionary<Guid, CouponHistoryRow> history,
        IReadOnlyList<SettledFact> settled, IReadOnlyList<PaidFact> paid, DateTimeOffset settledBefore)
    {
        var findings = new List<CrossStoreFinding>();
        var settlements = settled.ToDictionary(s => s.CouponId);
        var payouts = paid.ToDictionary(p => p.CouponId);
        foreach (var coupon in placed)
        {
            if (!history.TryGetValue(coupon.CouponId, out var row) || row.PlacedAt is null)
            {
                findings.Add(new(coupon.CouponId, "missingInHistory", "placement has the coupon; history never projected its placement"));
                continue;
            }

            if (row.Stake != coupon.Stake || row.PotentialPayout != coupon.PotentialPayout)
            {
                findings.Add(new(coupon.CouponId, "placementMismatch", $"stake {row.Stake}/{coupon.Stake}, potential payout {row.PotentialPayout}/{coupon.PotentialPayout} (history/placement)"));
            }

            if (settlements.TryGetValue(coupon.CouponId, out var settlement) && settlement.SettledAt < settledBefore)
            {
                if (row.SettlementVersion < settlement.Version)
                {
                    findings.Add(new(coupon.CouponId, "settlementBehind", $"history at version {row.SettlementVersion}, settlement at {settlement.Version} ({settlement.Outcome})"));
                }

                if (payouts.TryGetValue(coupon.CouponId, out var payout) && payout.PaidToDate != row.PaidToDate)
                {
                    findings.Add(new(coupon.CouponId, "paidMismatch", $"history paid {row.PaidToDate}, payout paid {payout.PaidToDate}"));
                }
            }
        }

        return findings;
    }
}
