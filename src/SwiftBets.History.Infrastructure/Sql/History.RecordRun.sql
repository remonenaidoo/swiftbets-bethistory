INSERT INTO history.integrity_runs (run_id, started_at, window_from, window_to, coupons_checked, findings)
VALUES (@RunId, @StartedAt, @WindowFrom, @WindowTo, @CouponsChecked, cardinality(@CouponIds));

INSERT INTO history.integrity_findings (run_id, coupon_id, problem, detail)
SELECT @RunId, f.coupon_id, f.problem, f.detail
FROM unnest(@CouponIds, @Problems, @Details) AS f(coupon_id, problem, detail)
ON CONFLICT DO NOTHING;

DELETE FROM history.integrity_runs WHERE started_at < now() - interval '30 days';
