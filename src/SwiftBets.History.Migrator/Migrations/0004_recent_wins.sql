CREATE INDEX IF NOT EXISTS ix_history_coupons_recent_wins ON history.coupons (updated_at DESC) WHERE paid_to_date > 0 AND status IN ('won', 'cashedOut');
