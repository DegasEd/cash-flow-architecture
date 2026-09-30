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
  |  local database transaction
  v
PostgreSQL
  ├── launch.entry
  └── launch.outbox_event
          |
          v
     Outbox Publisher
          |
          v
        Kafka
     entry-events
          |
          v
Consolidation Processor
          |
          v
PostgreSQL
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
- PostgreSQL local infrastructure
- pgAdmin
- Apache Kafka running in KRaft mode
- `entry-events` Kafka topic
- Kafka UI
- .NET Aspire AppHost
- shared Aspire service defaults
- health checks and observability foundation

The following components are part of the architecture but are not yet implemented:

- Outbox Publisher
- Consolidation Processor
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

The Outbox Publisher that drains these records to Kafka is the next application component to be implemented.

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

This provides the basis for correlating future flows across:

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

The next component is the Outbox Publisher.

Its responsibility is to:

```text
read unpublished launch.outbox_event records
        |
        v
publish them to Kafka / entry-events
        |
        v
mark successfully published records
```

Publication is intentionally independent from the Entry API.

This preserves the central resilience requirement of the architecture: failure of the asynchronous consolidation pipeline must not prevent financial entries from being durably accepted.