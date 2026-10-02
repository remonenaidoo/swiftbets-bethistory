SELECT run_id AS RunId, started_at AS StartedAt, window_from AS WindowFrom, window_to AS WindowTo, coupons_checked AS CouponsChecked
FROM history.integrity_runs
ORDER BY started_at DESC
LIMIT 1;

SELECT f.coupon_id AS CouponId, f.problem AS Problem, f.detail AS Detail
FROM history.integrity_findings f
WHERE f.run_id = (SELECT run_id FROM history.integrity_runs ORDER BY started_at DESC LIMIT 1)
ORDER BY f.problem, f.coupon_id
LIMIT 500;
