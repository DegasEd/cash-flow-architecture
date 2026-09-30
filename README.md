# Cash Flow Architecture

Event-driven cash flow solution focused on resilience, scalability and observability.

This repository implements a cash flow architecture designed around independent write and consolidation workloads.

The system accepts debit and credit entries, persists them durably and propagates changes asynchronously so that daily consolidation failures do not prevent new financial entries from being recorded.

The implementation intentionally favors explicit architectural decisions and simple components over unnecessary framework complexity.

---

## Architecture Overview

The solution is organized around independent applications with asynchronous integration through Kafka.

The main processing flow is:

```text
Client
  |
  v
CashFlow.Entry
  |
  | local database transaction
  v
PostgreSQL
  ├── launch.entry
  └── launch.outbox_event
          |
          v
     CashFlow.Outbox
          |
          v
        Kafka
     entry-events
          |
          v
CashFlow.ConsolidationProcessor
          |
          | local database transaction
          v
PostgreSQL
  ├── consolidation.processed_event
  └── consolidation.daily_consolidation
          |
          v
Consolidation Query API
```

The Entry application does not depend on the consolidation process being available.

Once an entry is accepted, the financial entry and its publication intent are persisted atomically using the Transactional Outbox pattern.

Event publication and consolidation processing are asynchronous.

The architecture therefore provides eventual consistency between the transactional entry model and the daily consolidated read model.

Detailed architectural documentation is available under:

```text
docs/architecture/
```

This includes the architecture blueprint, ADRs, C4 diagrams, logical data model and reliability/processing semantics.

---

## Current Implementation Status

The current implementation includes:

- Cash Flow Entry API
- layered Entry application structure
- PostgreSQL persistence
- Transactional Outbox persistence
- Outbox Publisher Worker
- asynchronous publication to Apache Kafka
- Consolidation Processor Worker
- idempotent event processing
- atomic daily consolidation updates
- manual Kafka offset commit after successful processing
- PostgreSQL local infrastructure
- pgAdmin
- Apache Kafka running in KRaft mode
- `entry-events` Kafka topic
- Kafka UI
- .NET Aspire AppHost
- shared Aspire service defaults
- health checks and observability foundation

The following component is part of the architecture but is not yet implemented:

- Consolidation Query API

---

## Repository Structure

```text
cash-flow-architecture/
├── code/
│   ├── CashFlow.Entry/
│   │   ├── src/
│   │   │   ├── CashFlow.Entry.Api/
│   │   │   ├── CashFlow.Entry.Core/
│   │   │   ├── CashFlow.Entry.Domain/
│   │   │   └── CashFlow.Entry.Repository/
│   │   └── tests/
│   │
│   ├── CashFlow.Outbox/
│   │   ├── src/
│   │   │   ├── CashFlow.Outbox.Worker/
│   │   │   ├── CashFlow.Outbox.Core/
│   │   │   ├── CashFlow.Outbox.Repository/
│   │   │   └── CashFlow.Outbox.Domain/
│   │   └── tests/
│   │
│   ├── CashFlow.ConsolidationProcessor/
│   │   ├── src/
│   │   │   ├── CashFlow.ConsolidationProcessor.Worker/
│   │   │   ├── CashFlow.ConsolidationProcessor.Core/
│   │   │   ├── CashFlow.ConsolidationProcessor.Repository/
│   │   │   └── CashFlow.ConsolidationProcessor.Domain/
│   │   └── tests/
│   │
│   ├── CashFlow.AppHost/
│   └── CashFlow.ServiceDefaults/
│
├── docker/
│   ├── kafka/
│   └── postgres/
│
├── docs/
│   └── architecture/
│
├── docker-compose.yml
└── README.md
```

Each business application owns its internal layers and dependencies.

Business/domain projects are intentionally not shared between services. Integration between independently deployable applications occurs through explicit event contracts rather than shared business assemblies.

---

## CashFlow.Entry

`CashFlow.Entry` is responsible for receiving and durably recording debit and credit entries.

Its current application flow is:

```text
HTTP Request
    |
    v
EntryController
    |
    v
IEntryService
    |
    v
EntryService
    |
    v
IEntryRepository
    |
    v
EntryRepository
    |
    v
PostgreSQL
```

The application follows a conventional layered structure:

```text
CashFlow.Entry.Api
CashFlow.Entry.Core
CashFlow.Entry.Repository
CashFlow.Entry.Domain
```

### Create Entry

The current endpoint is:

```http
POST /entries
```

Example request:

```json
{
  "amountInCents": 12550,
  "type": 1,
  "occurredAt": "2026-09-30"
}
```

Entry types:

```text
1 = Debit
2 = Credit
```

Example successful response:

```http
HTTP/1.1 201 Created
```

```json
{
  "id": "73158c28-5084-449e-a659-52156cddff4e",
  "amountInCents": 12550,
  "type": "Debit",
  "occurredAt": "2026-09-30",
  "createdAt": "2026-09-30T11:47:48.3337716Z"
}
```

Financial amounts are represented internally as integer cents, avoiding floating-point monetary calculations.

---

## Transactional Outbox

Creating an entry also creates an integration event in the outbox.

Both records are persisted in the same PostgreSQL transaction:

```text
BEGIN

INSERT launch.entry

INSERT launch.outbox_event

COMMIT
```

This guarantees that an accepted financial entry cannot be committed without also persisting its publication intent.

The outbox record contains:

```text
id
entry_id
event_type
payload
created_at
published_at
```

The current integration event type is:

```text
EntryCreated
```

`published_at` remains `NULL` until the event is successfully published.

Kafka availability is therefore not part of the synchronous Entry API transaction.

If Kafka becomes unavailable, financial entries can continue to be accepted while publication remains pending in the outbox.

---

## CashFlow.Outbox

`CashFlow.Outbox` is responsible for asynchronously publishing integration events persisted by the Entry application.

The application follows the same explicit layered organization:

```text
CashFlow.Outbox.Worker
CashFlow.Outbox.Core
CashFlow.Outbox.Repository
CashFlow.Outbox.Domain
```

Its processing flow is:

```text
launch.outbox_event
        |
        | published_at IS NULL
        v
Outbox Worker
        |
        v
IOutboxPublisherService
        |
        v
OutboxPublisherService
        |
        +----------------------+
        |                      |
        v                      v
IOutboxRepository         Apache Kafka
        |                 entry-events
        v                      |
PostgreSQL                     |
                               | broker acknowledgement
                               v
                     Mark event as published
                               |
                               v
                 launch.outbox_event.published_at
```

Pending events are read from PostgreSQL in batches.

Each event is published to the Kafka topic:

```text
entry-events
```

The Kafka message key is the financial entry identifier (`EntryId`).

An outbox event is marked as published only after Kafka acknowledges publication.

If Kafka publication fails, the event remains pending with:

```text
published_at = NULL
```

and can be retried by the worker.

The Worker continuously checks for pending events, while failures are logged without terminating the background process.

### Delivery Semantics

The Outbox Publisher intentionally does not claim distributed exactly-once processing.

A failure can occur after Kafka has acknowledged a message but before PostgreSQL records `published_at`.

In that scenario, the same event can be published again when the worker retries it.

This behavior is expected.

The architecture therefore uses:

```text
at-least-once delivery
+
idempotent downstream processing
```

Event redelivery is allowed.

Duplicate financial effects are not.

### End-to-End Validation

The Outbox Publisher has been validated against the real local infrastructure.

The validated flow was:

```text
launch.outbox_event
published_at = NULL
        |
        v
CashFlow.Outbox Worker
        |
        v
Kafka / entry-events
        |
        | broker acknowledgement
        v
launch.outbox_event
published_at = timestamp
```

Before the Worker was executed:

```text
PostgreSQL pending events: 1
Kafka messages:            0
```

After publication:

```text
Kafka messages:            1
PostgreSQL published_at:   recorded
```

The produced Kafka message was also inspected through Kafka UI, confirming the expected `EntryId` message key and serialized `EntryCreated` payload.

This validates the implemented path from durable transactional persistence to asynchronous event publication.

---

## CashFlow.ConsolidationProcessor

`CashFlow.ConsolidationProcessor` consumes `EntryCreated` integration events and incrementally maintains the daily consolidated balance.

The application follows the same explicit layered organization:

```text
CashFlow.ConsolidationProcessor.Worker
CashFlow.ConsolidationProcessor.Core
CashFlow.ConsolidationProcessor.Repository
CashFlow.ConsolidationProcessor.Domain
```

Its processing flow is:

```text
Kafka / entry-events
        |
        v
Consolidation Processor Worker
        |
        v
IConsolidationService
        |
        | deserialize + validate
        v
ConsolidationService
        |
        v
IConsolidationRepository
        |
        v
PostgreSQL transaction
        |
        +--> consolidation.processed_event
        |
        +--> consolidation.daily_consolidation
        |
        v
COMMIT
        |
        v
Kafka offset commit
```

The consumer belongs to the Kafka consumer group:

```text
cashflow-consolidation
```

Automatic offset commits are disabled.

A Kafka offset is committed only after the event has been successfully handled by the consolidation persistence transaction.

### Daily Balance Calculation

Credits increase the daily balance:

```text
Credit → +AmountInCents
```

Debits decrease the daily balance:

```text
Debit → -AmountInCents
```

The daily projection is maintained incrementally.

The Query side therefore does not need to recalculate all financial entries whenever a balance is requested.

For a new event, PostgreSQL performs an atomic UPSERT equivalent to:

```text
existing daily balance
+
signed event delta
=
new daily balance
```

The increment occurs inside PostgreSQL rather than through a read-modify-write operation in application memory.

This allows different events for the same date to be processed concurrently without requiring application-level distributed locks or a singleton processor.

### Idempotent Processing

Kafka provides at-least-once delivery semantics for this flow.

An event can therefore be delivered more than once.

Each integration event contains an `EventId`.

Before applying its financial effect, the Processor attempts to register that identifier in:

```text
consolidation.processed_event
```

`event_id` is unique.

Processing occurs as one local PostgreSQL transaction:

```text
BEGIN
    |
    v
INSERT processed_event
ON CONFLICT DO NOTHING
    |
    +--> EventId already exists
    |       |
    |       v
    |    no financial effect
    |
    +--> EventId is new
            |
            v
       atomic UPSERT
       daily_consolidation
            |
            v
COMMIT
```

If the `EventId` was already processed, the daily balance is not modified again.

This makes event redelivery harmless from the financial perspective.

### Kafka Offset Semantics

The processing order is intentionally:

```text
Kafka consume
      |
      v
PostgreSQL transaction
      |
      v
ProcessedEvent + DailyConsolidation
      |
      v
PostgreSQL COMMIT
      |
      v
Kafka offset commit
```

A process failure after consuming the event but before the PostgreSQL transaction commits causes the event to be delivered again.

A process failure after the PostgreSQL transaction commits but before the Kafka offset is committed can also cause redelivery.

In the second scenario, the persisted `EventId` identifies the event as already processed and prevents the daily balance from being modified twice.

The Kafka offset can then safely be committed.

This provides the intended guarantee:

```text
at-least-once delivery
+
idempotent financial effect
```

without requiring a distributed transaction between Kafka and PostgreSQL.

### End-to-End Validation

The Consolidation Processor has been validated against the real local Kafka and PostgreSQL infrastructure.

Before starting the Processor:

```text
consolidation.processed_event:      0 rows
consolidation.daily_consolidation:  0 rows
```

Kafka already contained a previously published `EntryCreated` event representing:

```text
Type:          Credit
AmountInCents: 5000
OccurredAt:    2026-09-30
```

After starting the Processor, the event was consumed and persisted successfully.

The resulting projection was:

```text
date        balance_in_cents
2026-09-30  5000
```

The corresponding event identifier was also registered in:

```text
consolidation.processed_event
```

The validated end-to-end flow is therefore:

```text
POST /entries
      |
      v
launch.entry
+
launch.outbox_event
      |
      v
CashFlow.Outbox
      |
      v
Kafka / entry-events
      |
      v
CashFlow.ConsolidationProcessor
      |
      v
processed_event
+
daily_consolidation
      |
      v
balance_in_cents = 5000
```

This validates the implemented asynchronous path from durable financial entry creation through Kafka publication to the materialized daily consolidation.

---

## Local Infrastructure

The local development environment is orchestrated through Docker Compose.

From the repository root:

```bash
docker compose up -d
```

Docker Compose starts the infrastructure required by the application while preserving already running services whenever their configuration does not require recreation.

All infrastructure components share the Docker network:

```text
cashflow-network
```

Current local infrastructure:

```text
PostgreSQL
pgAdmin
Apache Kafka
Kafka UI
```

---

## PostgreSQL

PostgreSQL is the primary persistence layer of the solution.

| Property | Value |
|---|---|
| Host | `localhost` |
| Port | `5432` |
| Database | `cashflow` |
| Username | `cashflow` |
| Password | `cashflow` |

The database is initialized automatically from:

```text
docker/postgres/init.sql
```

The current logical database organization uses two schemas:

```text
cashflow
├── launch
│   ├── entry
│   └── outbox_event
│
└── consolidation
    ├── daily_consolidation
    └── processed_event
```

The `launch` schema owns the financial entries and the transactional outbox used to publish integration events.

The `consolidation` schema contains the materialized daily balance projection and the processed-event registry used for idempotent event processing.

Although both schemas currently use the same PostgreSQL instance for operational simplicity, ownership remains separated by service boundary.

PostgreSQL health is monitored by Docker Compose using `pg_isready`.

---

## pgAdmin

pgAdmin is included in the local environment so the database can be inspected without requiring additional tools.

Open:

```text
http://localhost:5050
```

Login:

| Property | Value |
|---|---|
| Email | `admin@cashflow.dev` |
| Password | `admin` |

To register the PostgreSQL server inside pgAdmin, use:

| Property | Value |
|---|---|
| Host name/address | `postgres` |
| Port | `5432` |
| Maintenance database | `cashflow` |
| Username | `cashflow` |
| Password | `cashflow` |

> Inside the Docker network, services reach PostgreSQL through `postgres:5432`.
> Applications running directly on the host use `localhost:5432`.

---

## Apache Kafka

Apache Kafka provides asynchronous integration between the transactional Entry side and the consolidation side of the architecture.

The local environment uses Kafka in KRaft mode, without ZooKeeper.

Kafka is exposed locally on:

```text
localhost:9092
```

The current topic is:

```text
entry-events
```

It is created automatically by the local Kafka initialization process with:

```text
Partitions: 3
Replication factor: 1
```

Three partitions allow multiple consumer instances to process events concurrently as the consolidation workload scales.

The local single-broker environment intentionally uses replication factor `1`.

Production availability and replication requirements are separate deployment concerns documented by the architecture rather than simulated with unnecessary local broker replicas.

---

## Kafka UI

Kafka UI is included to make the messaging infrastructure directly observable during development and evaluation.

Open:

```text
http://localhost:8080
```

The UI can be used to inspect:

- broker state
- topics
- partitions
- messages
- consumer groups
- consumer lag

The configured local cluster is:

```text
cashflow
```

---

## .NET Aspire

.NET Aspire is used as the development orchestration and observability foundation for the .NET applications.

The repository contains:

```text
code/CashFlow.AppHost
code/CashFlow.ServiceDefaults
```

`CashFlow.AppHost` describes the distributed application topology used during development.

`CashFlow.ServiceDefaults` centralizes operational defaults such as health endpoints and OpenTelemetry configuration without sharing business logic between services.

To start the Aspire environment from the repository root:

```bash
aspire run --project code/CashFlow.AppHost/CashFlow.AppHost.csproj
```

Aspire displays the Dashboard URL when the AppHost starts.

The Entry API is currently registered as:

```text
entry-api
```

The Aspire Dashboard provides visibility into application resources, health state, logs and telemetry.

Ports may be dynamically assigned by Aspire during orchestration. The Dashboard should therefore be used as the authoritative source for Aspire-managed application endpoints.

---

## Health and Observability

Applications using `CashFlow.ServiceDefaults` expose the standard operational endpoints configured by the Aspire defaults.

The observability foundation includes OpenTelemetry integration for:

- structured logs
- distributed traces
- metrics

This provides the basis for correlating the complete processing flow:

```text
Entry API
    ->
Outbox Publisher
    ->
Kafka
    ->
Consolidation Processor
    ->
Consolidation Query API
```

---

## Checking Infrastructure

To inspect running Compose services:

```bash
docker compose ps
```

To include stopped one-shot services such as the Kafka topic initializer:

```bash
docker compose ps -a
```

To inspect all running Docker containers:

```bash
docker ps
```

---

## Resetting the Local Environment

The PostgreSQL initialization script runs when the PostgreSQL data volume is first created.

To completely recreate the local environment:

```bash
docker compose down -v
docker compose up -d
```

> **Warning:** this command deletes local Docker volumes and all data stored in them. It is intended only for resetting the local development environment.

---

## Processing Guarantees

The architecture deliberately does not claim distributed exactly-once delivery.

Instead, it is designed around:

```text
durable local persistence
+
transactional outbox
+
at-least-once event delivery
+
idempotent consumers
+
atomic projection updates
```

This means event redelivery is acceptable, but duplicate financial effects are not.

The implementation now demonstrates both sides of this processing model:

```text
Producer side
Entry + Outbox
same PostgreSQL transaction

Consumer side
ProcessedEvent + DailyConsolidation
same PostgreSQL transaction
```

Kafka remains outside both local database transactions.

The complete failure scenarios and processing guarantees are documented in:

```text
docs/architecture/reliability-and-processing-semantics.md
```

---

## Architectural Documentation

Architecture documentation is maintained under:

```text
docs/architecture/
```

The repository includes:

- architecture blueprint
- Architecture Decision Records (ADRs)
- C4 System Context diagram
- C4 Container diagram
- logical data model
- reliability and processing semantics
- transaction and idempotency diagrams

These documents capture both implemented decisions and explicitly documented evolution paths where implementing the complete production topology would exceed the scope of the exercise.

---

## Next Implementation Step

The next and final business application is the Consolidation Query API.

Its responsibility is to expose the materialized daily balance without recalculating financial entries during query execution.

The intended query flow is:

```text
GET /consolidations/{date}
        |
        v
Consolidation Query API
        |
        v
Query Service
        |
        v
Query Repository
        |
        v
consolidation.daily_consolidation
        |
        v
HTTP response
```

The Query API will read only the consolidation projection.

It will not query transactional entries or consume Kafka events.

For the currently validated projection, a query for:

```text
2026-09-30
```

is expected to return a balance equivalent to:

```json
{
  "date": "2026-09-30",
  "balanceInCents": 5000
}
```

Completing this component closes the full functional path from financial entry creation to daily consolidated balance retrieval.