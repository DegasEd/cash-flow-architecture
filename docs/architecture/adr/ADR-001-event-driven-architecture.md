# ADR-001 — Comunicação assíncrona orientada a eventos

**Status:** Accepted  
**Date:** 2026-09-29

## Contexto

A solução possui duas responsabilidades principais:

- Registrar e controlar lançamentos financeiros de débito e crédito;
- Disponibilizar o saldo consolidado diário.

O registro de novos lançamentos não poderá depender da disponibilidade do
processamento de consolidação.

Uma integração síncrona entre essas responsabilidades criaria dependência
temporal entre os serviços e permitiria que indisponibilidades ou degradações
do `Consolidation Service` afetassem o fluxo de lançamentos.

Além disso, o processamento da consolidação poderá ocorrer de forma
eventualmente consistente, desde que os lançamentos aceitos sejam preservados
e o saldo consolidado convirja após a recuperação de eventuais falhas.

## Decisão

A comunicação entre `Launch Service` e `Consolidation Service` será
assíncrona e orientada a eventos.

Após a aceitação e persistência de um lançamento, sua ocorrência será
propagada através de um evento de domínio/integração.

O `Consolidation Service` consumirá esses eventos independentemente do ciclo
de vida da requisição que originou o lançamento.

Não haverá chamada síncrona do `Launch Service` para o
`Consolidation Service` como parte do fluxo de aceitação de um lançamento.

## Consequências

### Positivas

- Indisponibilidade do `Consolidation Service` não impede novos lançamentos;
- Redução do acoplamento temporal entre os serviços;
- Possibilidade de processamento posterior de eventos acumulados durante
  períodos de indisponibilidade;
- Consumidores podem evoluir e escalar independentemente do fluxo de
  lançamentos;
- Novos consumidores poderão ser adicionados futuramente sem introduzir
  dependência síncrona no `Launch Service`.

### Trade-offs

- O saldo consolidado passa a possuir consistência eventual;
- A solução precisa tratar redelivery e idempotência;
- Falhas de processamento exigem mecanismos de recuperação e observabilidade;
- Aumenta a complexidade operacional em relação a uma integração síncrona.

## Alternativas consideradas

### Comunicação síncrona entre os serviços

O `Launch Service` poderia chamar diretamente o `Consolidation Service`
após registrar cada lançamento.

A alternativa foi rejeitada porque introduziria dependência de disponibilidade
entre os serviços no fluxo crítico de lançamento, contrariando o requisito de
que a indisponibilidade da consolidação não interrompa novos lançamentos.

### Consolidação exclusivamente sob demanda

O saldo poderia ser calculado percorrendo os lançamentos sempre que uma
consulta fosse realizada.

A alternativa foi rejeitada por transferir o custo de processamento para o
fluxo de leitura e dificultar a previsibilidade de desempenho à medida que o
volume de lançamentos crescer.

A solução utilizará uma projeção consolidada mantida de forma incremental.

## Decisões relacionadas

Este ADR define apenas o modelo de comunicação entre os serviços.

A tecnologia de mensageria, as garantias de publicação, a semântica de entrega,
a estratégia de idempotência e o modelo de persistência serão tratados em ADRs
específicos.
