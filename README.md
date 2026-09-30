# Cash Flow Architecture

Solução de fluxo de caixa orientada a eventos, desenvolvida em **.NET 10** com foco em **resiliência, escalabilidade e observabilidade**.

O projeto implementa o registro de lançamentos financeiros de débito e crédito e a manutenção assíncrona de um saldo consolidado diário.

A arquitetura separa o processamento transacional dos lançamentos do processamento de consolidação. Dessa forma, indisponibilidades temporárias na consolidação não impedem o registro de novos lançamentos financeiros.

---

## Solução

A solução é composta por quatro aplicações independentes:

- **CashFlow.Entry** — recebe e persiste os lançamentos financeiros;
- **CashFlow.Outbox** — publica assincronamente os eventos registrados através do Transactional Outbox;
- **CashFlow.ConsolidationProcessor** — processa os eventos e mantém incrementalmente a consolidação diária;
- **CashFlow.ConsolidationQuery** — disponibiliza o saldo diário previamente consolidado.

A integração entre os lados transacional e de consolidação ocorre de forma assíncrona através do **Apache Kafka**.

A solução utiliza **Transactional Outbox**, entrega **at-least-once** e processamento **idempotente**, permitindo redelivery de eventos sem duplicação de seus efeitos financeiros.

A implementação utiliza ainda **PostgreSQL**, **.NET Aspire**, **OpenTelemetry**, **Docker**, **Kubernetes** e **Minikube** como parte de sua infraestrutura, observabilidade e validação de escalabilidade.

O fluxo funcional completo encontra-se implementado e validado, desde o registro de um lançamento até a consulta do saldo diário consolidado.

---

## Princípios Arquiteturais

As principais decisões da solução foram orientadas pelos seguintes princípios:

- um lançamento financeiro aceito deve ser persistido de forma durável;
- a indisponibilidade da consolidação não deve impedir novos lançamentos;
- a integração entre os workloads deve permanecer desacoplada;
- redelivery de eventos pode ocorrer, mas não pode produzir efeitos financeiros duplicados;
- a consolidação deve ser incremental e previamente materializada;
- os componentes devem permitir escalabilidade horizontal;
- observabilidade deve fazer parte da solução desde sua execução local.

As decisões, trade-offs e garantias resultantes desses princípios estão detalhados na documentação arquitetural.

---

## Documentação

A documentação completa está disponível em [`docs/architecture`](./docs/architecture/).

| Documento | Descrição |
|---|---|
| [Blueprint da Arquitetura](./docs/architecture/blueprint.md) | Visão consolidada da solução, componentes, responsabilidades e principais decisões arquiteturais. |
| [Modelo C4](./docs/architecture/c4-model.md) | Representação visual da arquitetura, contexto do sistema e containers. |
| [Modelo de Dados](./docs/architecture/data-model.md) | Organização dos dados, schemas, tabelas e responsabilidades de persistência. |
| [Confiabilidade e Semântica de Processamento](./docs/architecture/reliability-and-processing-semantics.md) | Transactional Outbox, at-least-once, idempotência, transações, falhas, recuperação e convergência. |
| [ADRs](./docs/architecture/adr/) | Registro das principais decisões arquiteturais e seus respectivos trade-offs. |
| [Guia de Execução](./docs/architecture/guia-de-execucao.md) | Preparação e execução do ambiente, Aspire, Kubernetes, endpoints, testes, carga, escalabilidade, resiliência e troubleshooting. |

---

## Principais Decisões

As decisões arquiteturais formalizadas através de ADRs incluem:

- arquitetura orientada a eventos;
- Apache Kafka e contratos de eventos;
- Transactional Outbox;
- processamento idempotente da consolidação;
- CQRS pragmático.

A documentação registra não apenas as decisões adotadas, mas também suas motivações, consequências e caminhos de evolução.

---

## Sobre o Projeto

Este projeto foi desenvolvido como uma proposta arquitetural para um cenário de controle de fluxo de caixa que exige disponibilidade no registro de lançamentos e processamento independente da consolidação diária.

A implementação foi utilizada para validar empiricamente as principais decisões arquiteturais, incluindo persistência transacional, processamento assíncrono, idempotência, resiliência, observabilidade e escalabilidade horizontal.

Para reproduzir a solução e seus cenários de validação, consulte o **[Guia de Execução](./docs/architecture/guia-de-execucao.md)**.