CREATE SCHEMA IF NOT EXISTS history;

CREATE TABLE history.coupons
(
    coupon_id          uuid        PRIMARY KEY,
    punter_id          uuid        NOT NULL,
    status             text        NOT NULL,
    bet_type           text        NULL,
    stake              bigint      NULL,
    currency           char(3)     NOT NULL,
    total_odds         numeric(28, 6) NULL,
    potential_payout   bigint      NULL,
    legs               jsonb       NULL,
    placed_at          timestamptz NULL,
    settlement_version int         NOT NULL DEFAULT 0,
    payout             bigint      NULL,
    settled_at         timestamptz NULL,
    payout_version     int         NOT NULL DEFAULT 0,
    paid_to_date       bigint      NOT NULL DEFAULT 0,
    updated_at         timestamptz NOT NULL
);

CREATE INDEX ix_history_coupons_punter ON history.coupons (punter_id, placed_at DESC NULLS LAST);
