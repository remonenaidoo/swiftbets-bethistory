INSERT INTO history.coupons (coupon_id, punter_id, status, currency, payout_version, paid_to_date, updated_at)
VALUES (@CouponId, @PunterId, 'settled', @Currency, @Version, @PaidToDate, now())
ON CONFLICT (coupon_id) DO UPDATE
SET payout_version = EXCLUDED.payout_version, paid_to_date = EXCLUDED.paid_to_date, updated_at = now()
WHERE history.coupons.payout_version < EXCLUDED.payout_version;
