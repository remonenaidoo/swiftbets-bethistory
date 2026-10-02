SELECT coupon_id AS CouponId, status AS Status, bet_type AS BetType, stake AS Stake, currency AS Currency, total_odds AS TotalOdds,
       potential_payout AS PotentialPayout, legs::text AS LegsJson, placed_at AS PlacedAt, settlement_version AS SettlementVersion,
       payout AS Payout, paid_to_date AS PaidToDate, updated_at AS UpdatedAt, punter_id AS PunterId
FROM history.coupons
WHERE punter_id = @PunterId AND (NOT @OpenOnly OR status = 'open')
ORDER BY placed_at DESC NULLS LAST
LIMIT @Limit;
