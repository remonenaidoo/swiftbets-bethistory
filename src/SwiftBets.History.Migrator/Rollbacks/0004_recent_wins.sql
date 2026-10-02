-- Rolls back 0004_recent_wins. The recent-wins query still works, by scanning.
DROP INDEX IF EXISTS history.ix_history_coupons_recent_wins;
