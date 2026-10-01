-- Rolls back 0001_history. The read model is rebuilt by replaying the placement, settlement and payout topics.
DROP TABLE IF EXISTS history.coupons;
DROP SCHEMA IF EXISTS history;
