-- One punter's placed coupons newest first, filtered, keyset paged on (placed_at, coupon_id).
SELECT coupon_id AS CouponId, status AS Status, bet_type AS BetType, stake AS Stake, currency AS Currency, total_odds AS TotalOdds,
       potential_payout AS PotentialPayout, legs::text AS LegsJson, placed_at AS PlacedAt, settlement_version AS SettlementVersion,
       payout AS Payout, paid_to_date AS PaidToDate, updated_at AS UpdatedAt, punter_id AS PunterId
FROM history.coupons
WHERE punter_id = @PunterId AND placed_at IS NOT NULL
  AND (CAST(@Status AS text) IS NULL OR status = @Status)
  AND (CAST(@BetType AS text) IS NULL OR bet_type = @BetType)
  AND (CAST(@From AS timestamptz) IS NULL OR placed_at >= @From)
  AND (CAST(@To AS timestamptz) IS NULL OR placed_at < @To)
  AND (CAST(@BeforeAt AS timestamptz) IS NULL OR (placed_at, coupon_id) < (@BeforeAt, CAST(@BeforeId AS uuid)))
ORDER BY placed_at DESC, coupon_id DESC
LIMIT @Limit;
