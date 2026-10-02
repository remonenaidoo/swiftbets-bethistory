SELECT coupon_id AS CouponId, CASE WHEN placed_at IS NULL THEN 'settledWithoutPlacement' ELSE 'staleOpen' END AS Problem, updated_at AS Since
FROM history.coupons
WHERE (status = 'open' AND placed_at < now() - make_interval(hours => @StaleHours))
   OR (placed_at IS NULL AND updated_at < now() - make_interval(mins => @GraceMinutes))
ORDER BY updated_at
LIMIT 200;
