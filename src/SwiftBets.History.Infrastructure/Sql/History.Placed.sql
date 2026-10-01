INSERT INTO history.coupons (coupon_id, punter_id, status, bet_type, stake, currency, total_odds, potential_payout, legs, placed_at, updated_at)
VALUES (@CouponId, @PunterId, 'open', @BetType, @Stake, @Currency, @TotalOdds, @PotentialPayout, CAST(@Legs AS jsonb), @PlacedAt, now())
ON CONFLICT (coupon_id) DO UPDATE
SET bet_type = EXCLUDED.bet_type, stake = EXCLUDED.stake, total_odds = EXCLUDED.total_odds,
    potential_payout = EXCLUDED.potential_payout, legs = EXCLUDED.legs, placed_at = EXCLUDED.placed_at, updated_at = now()
WHERE history.coupons.placed_at IS NULL;
