# cash-flow-architecture
Event-driven cash flow solution focused on resilience, scalability and observability.

## Local Infrastructure

The local development environment is orchestrated through Docker Compose.

From the repository root:

```bash
docker compose up -d
```

All infrastructure components share the Docker network:

```text
cashflow-network
```

### PostgreSQL

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

PostgreSQL health is monitored by Docker Compose using `pg_isready`.

### pgAdmin

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

### Resetting the Local Database

The initialization script runs when the PostgreSQL data volume is first created.

To completely recreate the local database:

```bash
docker compose down -v
docker compose up -d
```

> **Warning:** this command deletes the local Docker volumes and all data stored in them. It is intended only for resetting the local development environment.
