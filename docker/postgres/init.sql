-- ============================================================
-- Cash Flow Architecture
-- PostgreSQL initialization
-- ============================================================

-- ------------------------------------------------------------
-- Schemas
-- ------------------------------------------------------------

CREATE SCHEMA IF NOT EXISTS launch;
CREATE SCHEMA IF NOT EXISTS consolidation;


-- ============================================================
-- LAUNCH
-- Source of truth for financial entries and transactional outbox
-- ============================================================

CREATE TABLE IF NOT EXISTS launch.entry
(
    id              UUID        PRIMARY KEY,
    amount_in_cents INTEGER     NOT NULL,
    type            VARCHAR(10) NOT NULL,
    occurred_at     DATE        NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL,

    CONSTRAINT ck_entry_amount_positive
        CHECK (amount_in_cents > 0),

    CONSTRAINT ck_entry_type
        CHECK (type IN ('DEBIT', 'CREDIT'))
);


CREATE TABLE IF NOT EXISTS launch.outbox_event
(
    id           UUID        PRIMARY KEY,
    entry_id     UUID        NOT NULL,
    event_type   VARCHAR(50) NOT NULL,
    payload      JSONB       NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL,
    published_at TIMESTAMPTZ NULL,

    CONSTRAINT fk_outbox_event_entry
        FOREIGN KEY (entry_id)
        REFERENCES launch.entry(id)
);


-- Used by the Outbox Publisher to efficiently find pending events.
CREATE INDEX IF NOT EXISTS ix_outbox_event_unpublished
    ON launch.outbox_event (created_at)
    WHERE published_at IS NULL;


-- ============================================================
-- CONSOLIDATION
-- Materialized daily projection and idempotency control
-- ============================================================

CREATE TABLE IF NOT EXISTS consolidation.daily_consolidation
(
    date             DATE        PRIMARY KEY,
    balance_in_cents INTEGER     NOT NULL,
    updated_at       TIMESTAMPTZ NOT NULL
);


CREATE TABLE IF NOT EXISTS consolidation.processed_event
(
    event_id     UUID        PRIMARY KEY,
    processed_at TIMESTAMPTZ NOT NULL
);