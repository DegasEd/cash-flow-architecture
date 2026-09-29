# ADR-003 — Transactional Outbox para publicação de eventos

**Status:** Accepted  
**Date:** 2026-09-29

## Contexto

Após aceitar um lançamento, o `Launch Service` precisa:

1. Persistir o lançamento financeiro;
2. Disponibilizar o evento correspondente para publicação na mensageria.

Executar essas operações de forma independente cria uma janela de
inconsistência.

Se o lançamento for persistido e a publicação falhar, o lançamento estará
registrado sem que o `Consolidation Service` seja informado.

Publicar o evento antes da persistência também não é aceitável, pois permitiria
a consolidação de um lançamento que posteriormente não fosse confirmado.

Não será utilizada transação distribuída entre banco de dados e mecanismo de
mensageria.

## Decisão

Será utilizado o padrão **Transactional Outbox**.

O lançamento e o registro do evento a ser publicado serão persistidos na mesma
transação do banco de dados do `Launch Service`.

Após o commit, um processo independente será responsável por localizar eventos
pendentes na Outbox, publicá-los no Kafka e registrar o sucesso da publicação.

Dessa forma, uma indisponibilidade temporária do Kafka não impedirá a aceitação
de novos lançamentos enquanto o `Launch Service` conseguir persistir sua
transação local.

Quando a mensageria estiver novamente disponível, os eventos pendentes poderão
ser publicados.

A publicação poderá ocorrer mais de uma vez em cenários de falha. A solução,
portanto, não dependerá da Outbox para eliminar duplicidades no consumo.

## Consequências

### Positivas

- lançamento e intenção de publicação são persistidos atomicamente;
- elimina a janela de perda entre persistência e publicação;
- indisponibilidade temporária do Kafka não implica perda de lançamentos
  aceitos;
- eventos pendentes podem ser recuperados e publicados posteriormente;
- não exige transação distribuída entre banco e Kafka.

### Trade-offs

- exige armazenamento e processamento da Outbox;
- a publicação passa a ser assíncrona;
- registros publicados precisam de estratégia de retenção ou limpeza;
- falhas entre publicação no Kafka e atualização da Outbox podem provocar
  republicação do mesmo evento.

## Alternativas consideradas

### Persistir e publicar diretamente no Kafka

A persistência do lançamento e a publicação seriam operações independentes.

A alternativa foi rejeitada porque uma falha entre as duas operações poderia
deixar um lançamento persistido sem o respectivo evento.

### Transação distribuída

Uma transação envolvendo banco de dados e mensageria poderia coordenar as duas
operações.

A alternativa foi rejeitada pelo aumento de acoplamento e complexidade
operacional, além de não ser necessária para os requisitos desta solução.

## Decisões relacionadas

- ADR-001 — Comunicação assíncrona orientada a eventos
- ADR-002 — Apache Kafka como mecanismo de mensageria
- ADR-004 — Semântica de entrega e idempotência
