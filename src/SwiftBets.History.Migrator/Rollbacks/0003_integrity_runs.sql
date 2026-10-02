-- Rolls back 0003_integrity_runs. Past run records are lost; the next run starts a fresh record.
DROP TABLE IF EXISTS history.integrity_findings;
DROP TABLE IF EXISTS history.integrity_runs;
