CREATE INDEX IF NOT EXISTS ix_history_coupons_open ON history.coupons (punter_id, placed_at DESC) WHERE status = 'open';
CREATE INDEX IF NOT EXISTS ix_history_coupons_integrity ON history.coupons (updated_at) WHERE status = 'open' OR placed_at IS NULL;
