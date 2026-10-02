CREATE TABLE history.integrity_runs
(
    run_id          uuid        PRIMARY KEY,
    started_at      timestamptz NOT NULL,
    window_from     timestamptz NOT NULL,
    window_to       timestamptz NOT NULL,
    coupons_checked int         NOT NULL,
    findings        int         NOT NULL
);

CREATE INDEX ix_history_integrity_runs_started ON history.integrity_runs (started_at DESC);

CREATE TABLE history.integrity_findings
(
    run_id    uuid NOT NULL REFERENCES history.integrity_runs (run_id) ON DELETE CASCADE,
    coupon_id uuid NOT NULL,
    problem   text NOT NULL,
    detail    text NOT NULL,
    PRIMARY KEY (run_id, coupon_id, problem)
);
