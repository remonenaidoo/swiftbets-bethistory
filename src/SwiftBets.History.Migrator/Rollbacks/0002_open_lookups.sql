-- Rolls back 0002_open_lookups. Open-bet and integrity queries still work, by scanning.
DROP INDEX IF EXISTS history.ix_history_coupons_integrity;
DROP INDEX IF EXISTS history.ix_history_coupons_open;
