# ADR-002 — Apache Kafka como mecanismo de mensageria

**Status:** Accepted  
**Date:** 2026-09-29

## Contexto

O ADR-001 definiu comunicação assíncrona orientada a eventos entre
`Launch Service` e `Consolidation Service`.

O mecanismo de mensageria deverá suportar:

- Desacoplamento temporal entre producer e consumers;
- Retenção de eventos durante indisponibilidades;
- Recuperação e reprocessamento;
- Processamento concorrente;
- Observabilidade do backlog;
- Escalabilidade horizontal dos consumers.

## Decisão

Será utilizado **Apache Kafka** como mecanismo de mensageria entre os serviços.

Os eventos de lançamento serão publicados em um tópico Kafka e processados
pelo `Consolidation Service` através de um consumer group.

A retenção dos eventos permitirá que indisponibilidades temporárias do
`Consolidation Service` não provoquem perda dos eventos ainda não processados.

O tópico será particionado para permitir processamento concorrente. O número
de partições estabelecerá o limite de paralelismo efetivo dos consumers
pertencentes ao mesmo consumer group.

Não será exigida ordenação global dos eventos.

Os contratos dos eventos serão explícitos e versionados no código da solução.
A introdução de infraestrutura específica para governança de schemas não faz
parte do escopo desta versão.

Os producers utilizarão os mecanismos de idempotência disponíveis no Kafka
para reduzir duplicidades decorrentes de retries de publicação.

Essa garantia não será considerada `exactly-once` fim a fim, pois o
processamento envolve persistência externa ao Kafka. A semântica de entrega e
a idempotência do processamento serão tratadas em ADR específico.

## Consequências

### Positivas

- Retenção permite recuperação e replay;
- Consumer groups e partições permitem processamento concorrente;
- Producer e consumers permanecem temporalmente desacoplados;
- Consumer lag fornece uma medida objetiva do backlog de processamento;
- Novos consumidores poderão ser adicionados sem alterar o fluxo original de
  publicação.

### Trade-offs

- Kafka adiciona infraestrutura e complexidade operacional;
- retenção e particionamento precisam ser configurados adequadamente;
- o paralelismo efetivo de um consumer group é limitado pela quantidade de
  partições;
- replay exige consumidores preparados para reprocessamento seguro.

## Alternativas consideradas

### RabbitMQ

RabbitMQ atenderia ao requisito de comunicação assíncrona.

Kafka foi escolhido por sua aderência ao modelo adotado de retenção, replay,
processamento particionado, consumer groups e observação de consumer lag.

### Schema Registry

A utilização de um Schema Registry foi considerada para governança e
compatibilidade dos contratos de eventos.

Para o escopo atual, com um fluxo simples entre producer e consumer, o custo
operacional adicional não foi considerado proporcional ao benefício.

Os contratos permanecerão explícitos e versionados no código. Um mecanismo
formal de governança de schemas poderá ser introduzido caso a solução evolua
para múltiplos producers, consumers ou equipes independentes.

## Decisões relacionadas

- ADR-001 — Comunicação assíncrona orientada a eventos
- ADR-003 — Garantia transacional de publicação
- ADR-004 — Semântica de entrega e idempotência
