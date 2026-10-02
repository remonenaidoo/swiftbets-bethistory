SELECT coupon_id AS CouponId, punter_id AS PunterId, bet_type AS BetType, paid_to_date AS PaidToDate, currency AS Currency, updated_at AS PaidAt
FROM history.coupons
WHERE paid_to_date > 0 AND status IN ('won', 'cashedOut')
ORDER BY updated_at DESC
LIMIT @Limit;
