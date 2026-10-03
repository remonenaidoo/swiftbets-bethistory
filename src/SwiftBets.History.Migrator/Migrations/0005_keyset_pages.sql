-- Keyset pages of a punter's bets walk (placed_at, coupon_id) descending.
CREATE INDEX IF NOT EXISTS ix_history_coupons_punter_keyset ON history.coupons (punter_id, placed_at DESC, coupon_id DESC) WHERE placed_at IS NOT NULL;
