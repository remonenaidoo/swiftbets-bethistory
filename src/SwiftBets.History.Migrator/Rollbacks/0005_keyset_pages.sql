-- Rolls back 0005_keyset_pages. Filtered pages still work, through the older punter index.
DROP INDEX IF EXISTS history.ix_history_coupons_punter_keyset;
