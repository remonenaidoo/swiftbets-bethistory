-- Accumulator boost fixed at placement, and the bonus settlement paid inside the payout.
ALTER TABLE history.coupons ADD COLUMN acca_boost_percent numeric(5, 2) NOT NULL DEFAULT 0;
ALTER TABLE history.coupons ADD COLUMN boost_bonus bigint NULL;
