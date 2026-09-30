# ADR-003 — Transactional Outbox para publicação de eventos

**Status:** Accepted  
**Date:** 2026-09-29

## Contexto

Após aceitar um lançamento, o `Launch Service` precisa garantir duas coisas:

1. persistir o lançamento financeiro;
2. registrar de forma durável a intenção de publicar o evento correspondente.

Executar a persistência do lançamento e a publicação no Kafka como operações independentes cria uma janela de inconsistência.

Se o lançamento for persistido e ocorrer uma falha antes da publicação, o lançamento estará registrado sem garantia de que o `Consolidation Service` receberá seu evento.

Publicar o evento antes da persistência também não é aceitável, pois permitiria o processamento de um lançamento que posteriormente poderia não ser confirmado.

Não será utilizada uma transação distribuída envolvendo o banco de dados do `Launch Service` e Kafka.

É necessário, portanto, garantir atomicidade dentro da fronteira controlada pelo `Launch Service`, preservando de forma durável tanto o lançamento quanto a intenção de publicação de seu evento.

## Decisão

Será utilizado o padrão **Transactional Outbox**.

O lançamento e seu respectivo `OutboxEvent` serão persistidos na mesma transação local do banco de dados do `Launch Service`.

Conceitualmente:

```text
BEGIN

    Launch
    OutboxEvent

COMMIT
```

As duas gravações formam uma única unidade transacional.

Se a transação for confirmada, tanto o lançamento quanto sua intenção de publicação estarão persistidos.

Se ocorrer uma falha antes do `COMMIT`, nenhuma das duas alterações será confirmada.

Após o commit, um processo independente, o `Outbox Publisher`, será responsável por localizar eventos pendentes, publicá-los no Kafka e registrar o sucesso da publicação.

A publicação no Kafka não faz parte da transação que persiste o lançamento.

Dessa forma, uma indisponibilidade temporária do Kafka não impede a aceitação de novos lançamentos enquanto o `Launch Service` conseguir persistir sua transação local.

Os eventos permanecem registrados na Outbox e poderão ser publicados quando a mensageria estiver novamente disponível.

### Semântica de publicação

A Transactional Outbox garante a persistência da **intenção de publicação**, mas não garante que um evento seja publicado exatamente uma vez.

Existe uma janela de falha entre:

```text
Kafka confirma publicação
        ↓
Outbox Publisher registra sucesso
```

Se o Publisher falhar nesse intervalo, o evento continuará aparecendo como pendente na Outbox.

Após sua recuperação, ele poderá publicar o mesmo evento novamente.

Portanto, republicação é uma condição esperada da arquitetura.

A solução assume entrega `at-least-once` e exige que o processamento realizado pelos consumidores seja idempotente.

O comportamento detalhado de publicação, redelivery, idempotência e recuperação é documentado em `docs/architecture/reliability-and-processing-semantics.md`.

## Consequências

### Positivas

- lançamento e intenção de publicação são persistidos atomicamente;
- um lançamento confirmado sempre possui uma intenção de publicação durável;
- indisponibilidade temporária do Kafka não implica perda de lançamentos aceitos;
- eventos pendentes podem ser recuperados e publicados posteriormente;
- o `Launch Service` não depende da disponibilidade imediata do Kafka para confirmar um lançamento;
- não exige transação distribuída entre banco de dados e Kafka.

### Trade-offs

- exige armazenamento e processamento adicional da Outbox;
- a publicação dos eventos passa a ser assíncrona;
- existe atraso possível entre a confirmação do lançamento e sua publicação;
- registros publicados precisam de estratégia de retenção ou limpeza;
- é necessário monitorar eventos pendentes e falhas do `Outbox Publisher`;
- falhas entre a publicação no Kafka e a atualização da Outbox podem provocar republicação do mesmo evento;
- consumidores precisam suportar redelivery e processamento idempotente.

## Alternativas consideradas

### Persistir e publicar diretamente no Kafka

A persistência do lançamento e a publicação seriam realizadas como operações independentes.

A alternativa foi rejeitada porque uma falha após a persistência do lançamento e antes da publicação poderia deixar um lançamento confirmado sem uma intenção durável de publicação.

Inverter a ordem também não resolve o problema, pois o evento poderia ser publicado antes da confirmação definitiva do lançamento.

### Transação distribuída

Uma transação envolvendo o banco de dados e a mensageria poderia tentar coordenar atomicamente as duas operações.

A alternativa foi rejeitada pelo aumento de acoplamento e complexidade operacional.

A solução adota transações locais dentro de cada fronteira e mecanismos explícitos de confiabilidade entre essas fronteiras.

## Decisões relacionadas

- ADR-001 — Comunicação assíncrona orientada a eventos
- ADR-002 — Apache Kafka como mecanismo de mensageria
- `docs/architecture/reliability-and-processing-semantics.md` — comportamento diante de falhas, redelivery, idempotência e concorrência
