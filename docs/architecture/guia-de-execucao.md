# Guia de Execução

Este documento apresenta o passo a passo para preparar, executar e validar a solução **Cash Flow Architecture** em ambiente local.

O objetivo é permitir que uma pessoa sem contato prévio com o projeto consiga clonar o repositório, preparar o ambiente, subir a infraestrutura e as aplicações, validar os endpoints e reproduzir os principais cenários utilizados na validação da solução.

Este guia possui caráter operacional. Decisões arquiteturais, modelos e detalhes de implementação estão documentados separadamente.

---

## 1. Documentação do Projeto

A documentação da solução está organizada em documentos com responsabilidades distintas.

| Documento | Descrição |
|---|---|
| [Blueprint da Arquitetura](./blueprint.md) | Visão consolidada da solução, seus componentes, responsabilidades e principais decisões arquiteturais. |
| [Modelo C4](./c4-model.md) | Visões de contexto e containers utilizadas para representar os limites e relacionamentos da solução. |
| [Modelo de Dados](./data-model.md) | Estrutura lógica dos dados, schemas, tabelas e responsabilidades de persistência. |
| [Confiabilidade e Semântica de Processamento](./reliability-and-processing-semantics.md) | Detalha Transactional Outbox, entrega at-least-once, idempotência, processamento assíncrono, falhas e convergência. |
| [ADRs](./adr/) | Registros das principais decisões arquiteturais adotadas na solução. |
| **Guia de Execução** | Documento atual. Preparação do ambiente, execução, endpoints, testes, carga, escalabilidade, resiliência e troubleshooting. |

Este documento evita repetir conceitos já registrados nos documentos arquiteturais e concentra-se em **como executar e validar a solução**.

---

## 2. Ambiente de Execução

A solução utiliza três recursos complementares para execução local:

- **Docker Compose** — dependências de infraestrutura;
- **.NET Aspire** — execução e observabilidade local dos componentes da aplicação;
- **Minikube** — execução em Kubernetes e validação de escalabilidade.

O Docker Compose executa:

- PostgreSQL;
- Apache Kafka;
- pgAdmin;
- Kafbat Kafka UI.

A aplicação é composta por:

- Entry API;
- Outbox Worker;
- Consolidation Processor;
- Consolidation Query API.

O projeto também possui:

- `CashFlow.AppHost` — orquestração local através do .NET Aspire;
- `CashFlow.ServiceDefaults` — configuração compartilhada de observabilidade e defaults utilizados pelos componentes.

### 2.1 Mapa do ambiente

| Componente | Endereço / acesso |
|---|---|
| PostgreSQL | `localhost:5432` |
| pgAdmin | `http://localhost:5050` |
| Kafka | `localhost:9092` |
| Kafbat Kafka UI | `http://localhost:8080` |
| Aspire Dashboard | endereço informado por `aspire run` |
| Kubernetes Dashboard | `minikube dashboard` |
| Entry API no Kubernetes | `http://localhost:8089` |
| Consolidation Query API no Kubernetes | `http://localhost:8088` |

As APIs executadas no Kubernetes são expostas localmente através de `port-forward`.

---

# Parte I — Preparação

## 3. Pré-requisitos

São necessários:

- Git;
- Docker;
- Docker Compose;
- .NET SDK 10;
- .NET Aspire CLI;
- kubectl;
- Minikube.

Os comandos deste documento consideram um shell compatível com Bash.

### 3.1 Git

```bash
git --version
```

### 3.2 Docker

```bash
docker --version
docker compose version
docker ps
```

### 3.3 .NET SDK

```bash
dotnet --version
```

A solução utiliza **.NET 10**.

### 3.4 Aspire CLI

```bash
aspire --version
```

Caso o Aspire esteja instalado localmente e não esteja disponível no `PATH`:

```bash
~/.aspire/bin/aspire --version
```

### 3.5 kubectl

```bash
kubectl version --client
```

### 3.6 Minikube

```bash
minikube version
```

### 3.7 Windows

Para execução em Windows, recomenda-se utilizar **WSL2 com integração ao Docker Desktop**.

Os comandos apresentados neste guia consideram sua execução dentro de um shell compatível com Bash.

---

## 4. Clonar o Repositório

```bash
git clone https://github.com/DegasEd/cash-flow-architecture.git
cd cash-flow-architecture
```

Todos os comandos seguintes consideram a **raiz do repositório** como diretório corrente.

---

# Parte II — Infraestrutura

## 5. Subir a Infraestrutura

PostgreSQL e Kafka são executados através do Docker Compose.

> **Importante:** execute esta etapa antes de direcionar o Docker CLI para o daemon do Minikube.

```bash
docker compose up -d
```

Validar:

```bash
docker compose ps
```

Os containers devem estar em execução antes de continuar.

---

## 6. Validar o PostgreSQL

O PostgreSQL está disponível em:

```text
Host:     localhost
Porta:    5432
Database: cashflow
Usuário:  cashflow
Senha:    cashflow
```

O banco é inicializado com os schemas:

```text
launch
consolidation
```

Principais tabelas:

```text
launch.entry
launch.outbox_event
consolidation.daily_consolidation
consolidation.processed_event
```

### pgAdmin

Acessar:

```text
http://localhost:5050
```

Credenciais:

```text
E-mail: admin@cashflow.dev
Senha:  admin
```

O servidor PostgreSQL da solução é registrado automaticamente.

---

## 7. Validar o Kafka

A interface Kafbat está disponível em:

```text
http://localhost:8080
```

Cluster:

```text
cashflow
```

Tópico utilizado pela aplicação:

```text
entry-events
```

O tópico é criado automaticamente com **3 partições**.

---

# Parte III — Testes Automatizados

## 8. Executar os Testes

### Entry

```bash
dotnet test \
  code/CashFlow.Entry/tests/CashFlow.Entry.Tests/CashFlow.Entry.Tests.csproj
```

### Consolidation Processor

```bash
dotnet test \
  code/CashFlow.ConsolidationProcessor/tests/CashFlow.ConsolidationProcessor.Tests/CashFlow.ConsolidationProcessor.Tests.csproj
```

### Consolidation Query

```bash
dotnet test \
  code/CashFlow.ConsolidationQuery/tests/CashFlow.ConsolidationQuery.Tests/CashFlow.ConsolidationQuery.Tests.csproj
```

A suíte atual contém **6 testes**:

| Componente | Testes |
|---|---:|
| Entry | 2 |
| Consolidation Processor | 2 |
| Consolidation Query | 2 |
| **Total** | **6** |

Os testes automatizados concentram-se nos comportamentos considerados mais relevantes. As validações de integração, carga, escalabilidade e resiliência são executadas nas próximas etapas.

---

# Parte IV — Execução Local com .NET Aspire

## 9. Executar a Solução com Aspire

A solução possui um **.NET Aspire AppHost** para execução e observabilidade local dos quatro componentes da aplicação.

Esta é a forma mais simples de executar toda a aplicação durante desenvolvimento ou inspeção local.

O Aspire não substitui o ambiente Kubernetes utilizado posteriormente para validar deployment, recursos e Horizontal Pod Autoscaler.

### 9.1 Iniciar o AppHost

A partir da raiz do repositório:

```bash
aspire run \
  --project code/CashFlow.AppHost/CashFlow.AppHost.csproj
```

Caso o Aspire CLI não esteja disponível no `PATH`:

```bash
~/.aspire/bin/aspire run \
  --project code/CashFlow.AppHost/CashFlow.AppHost.csproj
```

O AppHost inicia os seguintes recursos:

```text
entry-api
outbox-worker
consolidation-processor
consolidation-query-api
```

O terminal informará o endereço do **Aspire Dashboard**.

Abrir o endereço apresentado e confirmar que os quatro recursos estão com estado:

```text
Running
```

O Dashboard permite acompanhar centralmente:

- estado dos recursos;
- logs;
- métricas;
- traces disponibilizados pela instrumentação configurada através do `CashFlow.ServiceDefaults`.

Para encerrar:

```text
Ctrl+C
```

> Os cenários de Kubernetes, HPA e scale-out apresentados nas próximas etapas devem ser executados através do Minikube.

---

# Parte V — Kubernetes

## 10. Iniciar o Minikube

```bash
minikube start --driver=docker
```

Validar:

```bash
minikube status
```

Estado esperado:

```text
host:       Running
kubelet:    Running
apiserver:  Running
kubeconfig: Configured
```

Validar o node:

```bash
kubectl get nodes
```

O node deve estar com status:

```text
Ready
```

---

## 11. Habilitar o Metrics Server

O Horizontal Pod Autoscaler depende das métricas do Kubernetes.

```bash
minikube addons enable metrics-server
```

Validar:

```bash
kubectl top nodes
```

Caso as métricas ainda não estejam disponíveis:

```bash
kubectl get pods -n kube-system
```

Aguardar a inicialização do `metrics-server` e executar novamente:

```bash
kubectl top nodes
```

---

## 12. Construir as Imagens

A solução utiliza um único `Dockerfile` multi-target.

Targets disponíveis:

```text
entry
outbox
consolidation-processor
consolidation-query
```

Direcionar o Docker CLI para o daemon utilizado pelo Minikube:

```bash
eval $(minikube docker-env)
```

Construir:

```bash
docker build --target entry \
  -t cashflow-entry:local .

docker build --target outbox \
  -t cashflow-outbox:local .

docker build --target consolidation-processor \
  -t cashflow-consolidation-processor:local .

docker build --target consolidation-query \
  -t cashflow-consolidation-query:local .
```

Validar:

```bash
docker images | grep cashflow
```

Devem existir:

```text
cashflow-entry
cashflow-outbox
cashflow-consolidation-processor
cashflow-consolidation-query
```

---

## 13. Subir as Aplicações

```bash
kubectl apply -f k8s/cashflow.yaml
```

Validar:

```bash
kubectl get deployments
kubectl get pods
```

Os quatro workloads devem atingir o estado `Running`:

```text
consolidation-processor   1/1   Running
consolidation-query       1/1   Running
entry                     1/1   Running
outbox                    1/1   Running
```

Para acompanhar visualmente:

```bash
minikube dashboard
```

---

# Parte VI — Endpoints e Validação Funcional

## 14. Expor as APIs

### Entry API

Em um terminal separado:

```bash
kubectl port-forward service/entry 8089:80
```

Endpoint:

```text
http://localhost:8089
```

### Consolidation Query API

Em outro terminal:

```bash
kubectl port-forward service/consolidation-query 8088:80
```

Endpoint:

```text
http://localhost:8088
```

Os processos de `port-forward` devem permanecer em execução durante as validações.

---

## 15. Entry API

### POST `/entries`

Endpoint:

```text
POST http://localhost:8089/entries
```

Tipos disponíveis:

```text
1 = Debit
2 = Credit
```

Exemplo:

```bash
curl -i -X POST http://localhost:8089/entries \
  -H "Content-Type: application/json" \
  -d '{
    "amountInCents": 100,
    "type": 2,
    "occurredAt": "2026-09-30"
  }'
```

Resultado esperado:

```text
HTTP/1.1 201 Created
```

A operação persiste o lançamento e seu evento de Outbox na mesma transação.

---

## 16. Consolidation Query API

### GET `/consolidations/{date}`

Endpoint:

```text
GET http://localhost:8088/consolidations/{date}
```

Exemplo:

```bash
curl -i \
  http://localhost:8088/consolidations/2026-09-30
```

Para uma data existente:

```text
HTTP/1.1 200 OK
```

Exemplo de resposta:

```json
{
  "date": "2026-09-30",
  "balanceInCents": 100,
  "updatedAt": "<timestamp-utc>"
}
```

Para uma data sem consolidação:

```bash
curl -i \
  http://localhost:8088/consolidations/2026-09-29
```

Resultado esperado:

```text
HTTP/1.1 404 Not Found
```

---

## 17. Validar a Convergência

No pgAdmin:

```sql
SELECT
    (SELECT COUNT(*) FROM launch.entry) AS entries,
    (SELECT COUNT(*) FROM launch.outbox_event) AS outbox_events,
    (
        SELECT COUNT(*)
        FROM launch.outbox_event
        WHERE published_at IS NULL
    ) AS pending_outbox,
    (
        SELECT COUNT(*)
        FROM consolidation.processed_event
    ) AS processed_events,
    (
        SELECT balance_in_cents
        FROM consolidation.daily_consolidation
        WHERE date = DATE '2026-09-30'
    ) AS balance_in_cents;
```

Após o processamento assíncrono:

```text
pending_outbox = 0
```

O lançamento deve possuir seu evento processado e o saldo consolidado deve refletir seu efeito financeiro.

A consolidação é eventualmente consistente. Portanto, pode existir um pequeno intervalo entre a confirmação do lançamento e sua disponibilidade no modelo consolidado.

---

# Parte VII — Carga e Escalabilidade

## 18. Preparar o k6

Os testes de carga utilizam **k6 através de Docker**. Não é necessária uma instalação local adicional.

Como o Docker CLI foi anteriormente direcionado para o daemon do Minikube, retornar ao Docker do host:

```bash
eval $(minikube docker-env -u)
```

Validar:

```bash
docker ps
```

---

## 19. Teste de Carga da Consolidation Query

Cenário:

```text
tests/load/consolidation-query.js
```

Carga:

```text
50 requisições por segundo
durante 2 minutos
```

Confirmar que o `port-forward` da Query permanece ativo em `localhost:8088`.

Executar:

```bash
docker run --rm \
  --network host \
  -v "$PWD/tests/load:/scripts:ro" \
  grafana/k6 run /scripts/consolidation-query.js
```

Volume esperado:

```text
aproximadamente 6.000 requisições
```

Critérios configurados:

```text
request loss <= 5%
HTTP failures <= 5%
```

### Resultado de referência

A execução realizada durante a validação da solução apresentou:

```text
Requisições:       6.000
Taxa:              50 req/s
Perda:             0,00%
Falhas HTTP:       0,00%
Checks:            100% aprovados
Latência p95:      1,91 ms
```

Os valores de latência são específicos do ambiente utilizado durante a execução de referência.

---

## 20. Teste de Carga da Entry API

Antes do teste, registrar o baseline:

```sql
SELECT
    (SELECT COUNT(*) FROM launch.entry) AS entries,
    (SELECT COUNT(*) FROM launch.outbox_event) AS outbox_events,
    (
        SELECT COUNT(*)
        FROM launch.outbox_event
        WHERE published_at IS NULL
    ) AS pending_outbox,
    (
        SELECT COUNT(*)
        FROM consolidation.processed_event
    ) AS processed_events,
    (
        SELECT balance_in_cents
        FROM consolidation.daily_consolidation
        WHERE date = DATE '2026-09-30'
    ) AS balance_in_cents;
```

Cenário:

```text
tests/load/entry.js
```

Carga:

```text
50 POST/s
durante 2 minutos
```

Cada requisição cria um `Credit` de 100 centavos.

Executar:

```bash
docker run --rm \
  --network host \
  -v "$PWD/tests/load:/scripts:ro" \
  grafana/k6 run /scripts/entry.js
```

Durante a execução:

```bash
kubectl get hpa -w
```

Opcionalmente:

```bash
kubectl get pods -w
```

### Resultado de referência

```text
Requisições:       6.001
HTTP 201:          6.001
Perda:             0,00%
Falhas HTTP:       0,00%
Latência p95:      3,24 ms
```

Durante esta execução, a Entry API escalou automaticamente:

```text
1 Pod → 5 Pods
```

O scale-out ocorreu em função da utilização de CPU, sem alteração do threshold para provocar artificialmente o comportamento.

---

## 21. Validar o Resultado da Carga

Após o término do teste, aguardar a convergência e executar novamente a consulta de baseline.

Para `N` requisições aceitas, são esperados:

```text
entries           +N
outbox_events     +N
processed_events  +N
pending_outbox     0
```

Como cada lançamento possui 100 centavos:

```text
delta do saldo = N × 100 centavos
```

Na execução de referência:

```text
Entries:           +6.001
Outbox Events:     +6.001
Processed Events:  +6.001
Saldo:             +600.100 centavos
Pending Outbox:    0
```

A validação deve utilizar os **deltas**, e não valores absolutos do banco.

---

# Parte VIII — Resiliência

## 22. Indisponibilidade do Consolidation Processor

Interromper temporariamente:

```bash
kubectl scale deployment/consolidation-processor \
  --replicas=0
```

Confirmar:

```bash
kubectl get pods
```

Criar um lançamento:

```bash
curl -i -X POST http://localhost:8089/entries \
  -H "Content-Type: application/json" \
  -d '{
    "amountInCents": 100,
    "type": 2,
    "occurredAt": "2026-09-30"
  }'
```

Resultado esperado:

```text
HTTP/1.1 201 Created
```

A Entry API permanece disponível independentemente da indisponibilidade do Processor.

Restaurar:

```bash
kubectl scale deployment/consolidation-processor \
  --replicas=1
```

Acompanhar:

```bash
kubectl get pods -w
```

Após a recuperação, validar novamente a convergência através da consulta SQL apresentada anteriormente.

---

# Parte IX — Inspeção e Troubleshooting

## 23. Comandos Úteis do Kubernetes

```bash
kubectl get deployments
kubectl get pods
kubectl get hpa
kubectl top pods
```

Logs:

```bash
kubectl logs deployment/entry
kubectl logs deployment/outbox
kubectl logs deployment/consolidation-processor
kubectl logs deployment/consolidation-query
```

---

## 24. Inspeção do Kafka

Acessar:

```text
http://localhost:8080
```

Navegar para:

```text
cashflow
└── Topics
    └── entry-events
```

As mensagens publicadas pelo Outbox podem ser inspecionadas através da interface.

---

## 25. Inspeção do PostgreSQL

Últimos lançamentos:

```sql
SELECT *
FROM launch.entry
ORDER BY created_at DESC
LIMIT 20;
```

Eventos pendentes:

```sql
SELECT *
FROM launch.outbox_event
WHERE published_at IS NULL
ORDER BY created_at;
```

Consolidações:

```sql
SELECT *
FROM consolidation.daily_consolidation
ORDER BY date DESC;
```

Eventos processados:

```sql
SELECT COUNT(*)
FROM consolidation.processed_event;
```

---

## 26. `ErrImageNeverPull`

Confirmar que as imagens existem no daemon utilizado pelo Minikube:

```bash
eval $(minikube docker-env)
docker images | grep cashflow
```

Caso necessário:

```bash
minikube image load <nome-da-imagem>:local
```

---

## 27. HPA apresenta `<unknown>`

Confirmar:

```bash
minikube addons enable metrics-server
```

Validar:

```bash
kubectl top nodes
kubectl top pods
```

Pode existir um pequeno intervalo até que o `metrics-server` comece a fornecer métricas.

---

## 28. Port-forward deixou de responder

Entry:

```bash
kubectl port-forward service/entry 8089:80
```

Query:

```bash
kubectl port-forward service/consolidation-query 8088:80
```

---

## 29. Pod em `CrashLoopBackOff`

Identificar:

```bash
kubectl get pods
```

Inspecionar:

```bash
kubectl describe pod <nome-do-pod>
```

Logs:

```bash
kubectl logs <nome-do-pod>
```

Caso tenha reiniciado:

```bash
kubectl logs <nome-do-pod> --previous
```

---

# Parte X — Encerramento

## 30. Remover os Workloads

```bash
kubectl delete -f k8s/cashflow.yaml
```

---

## 31. Parar o Minikube

Preservando o cluster:

```bash
minikube stop
```

Removendo o cluster:

```bash
minikube delete
```

---

## 32. Retornar ao Docker do Host

Caso necessário:

```bash
eval $(minikube docker-env -u)
```

---

## 33. Parar a Infraestrutura

```bash
docker compose down
```

Para remover também os volumes:

```bash
docker compose down -v
```

> **Atenção:** a opção `-v` remove os dados persistidos localmente pelo PostgreSQL.

---

# Checklist Final

Ao concluir o procedimento deve ser possível verificar que:

- PostgreSQL e Kafka estão disponíveis;
- os 6 testes automatizados são executados com sucesso;
- os quatro componentes podem ser executados e observados através do .NET Aspire;
- os quatro workloads estão em execução no Kubernetes;
- a Entry API aceita lançamentos;
- os eventos são publicados e processados assincronamente;
- a Consolidation Query retorna o saldo diário;
- o cenário de 50 requisições por segundo atende ao limite de perda estabelecido;
- a Entry API permanece disponível durante a indisponibilidade do processamento da consolidação;
- o HPA responde ao aumento de utilização;
- após o processamento assíncrono, o sistema converge sem eventos pendentes no Outbox.

Este guia encerra-se na validação operacional da solução. Para os fundamentos, decisões e detalhes arquiteturais observados durante estes cenários, consulte os documentos relacionados na [seção de documentação](#1-documentação-do-projeto).
