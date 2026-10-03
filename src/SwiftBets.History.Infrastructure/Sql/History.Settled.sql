INSERT INTO history.coupons (coupon_id, punter_id, status, currency, settlement_version, payout, settled_at, boost_bonus, updated_at)
VALUES (@CouponId, @PunterId, @Status, @Currency, @Version, @Payout, @SettledAt, @BoostBonus, now())
ON CONFLICT (coupon_id) DO UPDATE
SET status = EXCLUDED.status, settlement_version = EXCLUDED.settlement_version, payout = EXCLUDED.payout,
    settled_at = EXCLUDED.settled_at, boost_bonus = EXCLUDED.boost_bonus, updated_at = now()
WHERE history.coupons.settlement_version < EXCLUDED.settlement_version;
