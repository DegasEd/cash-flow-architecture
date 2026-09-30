# ADR-004 — Processamento idempotente e atualização concorrente do consolidado

**Status:** Accepted  
**Date:** 2026-09-29

## Contexto

A comunicação entre o `Launch Service` e o `Consolidation Service` é assíncrona e utiliza Kafka.

Conforme definido nos ADRs anteriores, a publicação utiliza Transactional Outbox e a arquitetura assume que um mesmo evento pode ser publicado ou entregue mais de uma vez.

Portanto, o `Consolidation Service` precisa lidar corretamente com dois problemas distintos:

1. o mesmo evento sendo processado mais de uma vez;
2. eventos diferentes sendo processados concorrentemente sobre o mesmo consolidado diário.

Além disso, a projeção `DailyConsolidation` pode ainda não existir quando o primeiro evento de determinado dia for processado.

A solução precisa garantir que:

- redelivery não produza novamente o mesmo efeito financeiro;
- eventos diferentes possam ser processados concorrentemente;
- atualizações concorrentes não provoquem `lost update`;
- a criação concorrente do primeiro consolidado diário não produza registros duplicados nem perda de efeitos;
- o processamento permaneça horizontalmente escalável;
- não seja necessário introduzir lock distribuído ou serialização global dos consumidores.

Não será assumida uma garantia distribuída de `exactly-once` entre Kafka e a persistência do `Consolidation Service`.

## Decisão

A solução adotará processamento `at-least-once` com consumidor idempotente e atualização atômica da projeção diária.

Cada evento possuirá um `EventId` único.

O `Consolidation Service` manterá um registro `ProcessedEvent` para identificar os eventos que já produziram efeito financeiro.

`ProcessedEvent.EventId` possuirá uma restrição de unicidade:

```text
UNIQUE(EventId)
```

O registro do evento processado e a alteração correspondente em `DailyConsolidation` serão executados na mesma transação local.

Conceitualmente:

```text
BEGIN

    tentar registrar ProcessedEvent(EventId)

    se EventId foi registrado:
        criar ou incrementar DailyConsolidation

COMMIT
```

Dessa forma, o registro de que um evento foi processado não pode ser confirmado sem que seu efeito financeiro correspondente também seja confirmado.

Da mesma forma, o efeito financeiro não pode ser confirmado sem o registro do `ProcessedEvent`.

### Mesmo evento processado concorrentemente

Quando dois workers tentarem processar o mesmo evento:

```text
Worker A → Event ABC
Worker B → Event ABC
```

ambos poderão tentar registrar:

```text
ProcessedEvent(EventId = ABC)
```

A restrição `UNIQUE(EventId)` será utilizada como mecanismo de arbitragem da concorrência.

Somente uma tentativa poderá registrar o `EventId` e produzir seu efeito financeiro.

A outra tentativa reconhecerá que o evento já foi processado e não alterará o consolidado.

Portanto:

```text
múltiplas entregas
        ↓
um EventId
        ↓
um efeito financeiro
```

A arquitetura permite redelivery, mas não permite que o mesmo evento produza novamente seu efeito financeiro.

### Eventos diferentes processados concorrentemente

Eventos diferentes são legítimos e precisam produzir seus respectivos efeitos.

Considere:

```text
Event ABC = +100
Event DEF = +50
```

Como:

```text
ABC != DEF
```

ambos podem ser registrados em `ProcessedEvent`.

A aplicação não utilizará uma estratégia de `read-modify-write` para calcular o novo saldo.

Uma implementação como:

```text
ler saldo
calcular novo saldo na aplicação
gravar novo saldo
```

permitiria que dois workers lessem simultaneamente o mesmo valor e posteriormente sobrescrevessem o resultado um do outro, produzindo `lost update`.

A alteração do saldo será realizada como uma operação atômica sobre o valor persistido.

Conceitualmente:

```text
Balance = Balance + delta
```

Assim, eventos diferentes podem alterar concorrentemente o mesmo consolidado sem exigir serialização global dos consumidores.

Como os efeitos utilizados para cálculo do saldo são deltas aditivos, sua aplicação é comutativa.

Para esse cálculo, não é necessário estabelecer ordenação global dos eventos.

### Criação concorrente do primeiro consolidado diário

Também é necessário tratar o cenário em que ainda não existe um `DailyConsolidation` para a data processada.

Considere:

```text
DailyConsolidation(Date) = inexistente

Worker A → Event ABC +100
Worker B → Event DEF  +50
```

Uma estratégia baseada em consultar previamente a existência da projeção e posteriormente decidir entre `INSERT` e `UPDATE` introduziria uma condição de corrida.

Os dois workers poderiam observar simultaneamente que o registro ainda não existe e tentar criá-lo.

Por isso, `DailyConsolidation` possuirá uma chave única correspondente à sua data:

```text
UNIQUE(Date)
```

Considerando que o escopo atual possui um único lojista, a data é suficiente para identificar a projeção diária.

A persistência utilizará uma operação atômica de **create-or-increment**:

```text
registro não existe
        ↓
criar com o delta

registro já existe
        ↓
incrementar com o delta
```

Na implementação com PostgreSQL, essa operação poderá ser realizada utilizando `UPSERT`, por meio de `INSERT ... ON CONFLICT`.

Exemplo conceitual:

```sql
INSERT INTO DailyConsolidation (Date, Balance)
VALUES (@date, @amount)

ON CONFLICT (Date)
DO UPDATE
SET Balance = DailyConsolidation.Balance + EXCLUDED.Balance;
```

A restrição `UNIQUE(Date)` permite que o banco arbitre a criação concorrente da projeção.

Se dois eventos diferentes forem processados simultaneamente quando a projeção ainda não existe, um deles criará o registro e o outro aplicará seu delta sobre o registro existente.

Independentemente de qual operação seja executada primeiro:

```text
ABC primeiro:

INSERT 100
UPDATE +50
→ 150
```

ou:

```text
DEF primeiro:

INSERT 50
UPDATE +100
→ 150
```

o resultado final permanece correto.

A mesma operação de `create-or-increment` pode, portanto, tratar tanto a criação inicial quanto as atualizações posteriores da projeção.

## Fronteira transacional

As garantias de idempotência e atualização do consolidado pertencem à mesma fronteira transacional do `Consolidation Service`.

Para cada evento:

```text
BEGIN

    ProcessedEvent(EventId)

    DailyConsolidation create-or-increment

COMMIT
```

Se ocorrer uma falha antes do `COMMIT`, nenhuma das alterações será confirmada.

Se a transação for confirmada e o worker falhar antes do avanço do offset do consumer group no Kafka, o evento poderá ser entregue novamente.

Nesse caso, a restrição de unicidade de `ProcessedEvent.EventId` impedirá que seu efeito financeiro seja aplicado novamente.

Não existe uma transação distribuída envolvendo Kafka e o banco de dados do `Consolidation Service`.

A consistência é obtida por meio de redelivery, idempotência e transações locais.

## Decisões de concorrência

A solução distingue explicitamente três cenários.

### Mesmo evento — ABC × ABC

Problema:

```text
redelivery / processamento concorrente
do mesmo evento
```

Garantia:

```text
UNIQUE(EventId)
        +
ProcessedEvent e efeito financeiro
na mesma transação
```

Resultado:

```text
um único efeito financeiro
```

### Eventos diferentes com projeção existente — ABC × DEF

Problema:

```text
dois eventos legítimos alterando
concorrentemente o mesmo saldo
```

Garantia:

```text
incremento atômico
Balance = Balance + delta
```

Resultado:

```text
nenhum lost update
```

### Eventos diferentes sem projeção existente — ABC × DEF

Problema:

```text
dois eventos legítimos tentando produzir
o primeiro consolidado do dia
```

Garantia:

```text
UNIQUE(Date)
      +
atomic create-or-increment
```

Resultado:

```text
uma projeção diária
        +
todos os deltas aplicados
```

## Estratégias não adotadas

### Redis como autoridade de idempotência

Foi considerada a utilização de Redis para registrar os `EventId` processados.

A alternativa criaria duas persistências independentes para representar a conclusão do processamento:

```text
Redis
  +
DailyConsolidation
```

Uma falha entre a atualização dessas persistências poderia produzir inconsistência.

Se o evento fosse registrado no Redis antes da alteração do consolidado, uma falha poderia fazer com que uma nova entrega fosse descartada sem que o efeito financeiro tivesse sido aplicado.

Se o consolidado fosse atualizado antes do Redis, uma falha poderia permitir que uma nova entrega aplicasse o mesmo efeito novamente.

A alternativa foi rejeitada porque `ProcessedEvent` pode permanecer na mesma transação local que altera `DailyConsolidation`, evitando uma fronteira adicional de consistência.

### Lock distribuído

Não será utilizado lock distribuído para serializar alterações do consolidado.

A unicidade de `EventId`, a chave única da projeção diária e as operações atômicas realizadas pela persistência fornecem as garantias necessárias sem introduzir coordenação distribuída adicional.

### Worker único

Não será utilizado um único worker como mecanismo de garantia de consistência.

A estratégia impediria o processamento concorrente e limitaria desnecessariamente a capacidade de escala horizontal do `Consolidation Service`.

### Ordenação global dos eventos

Não será exigida ordenação global dos eventos para o cálculo do saldo diário.

Os efeitos utilizados na consolidação são deltas aditivos e, portanto, comutativos.

A correção do saldo não depende da ordem em que eventos diferentes sejam aplicados.

## Consequências

### Positivas

- redelivery é suportado sem duplicação do efeito financeiro;
- workers podem processar eventos concorrentemente;
- o mesmo evento não produz efeito financeiro mais de uma vez;
- eventos diferentes não provocam `lost update`;
- a primeira projeção diária pode ser criada concorrentemente com segurança;
- a mesma operação pode tratar criação e atualização da projeção;
- não exige lock distribuído;
- não exige worker único;
- não exige ordenação global para o cálculo do saldo;
- mantém compatibilidade com escala horizontal dos consumidores;
- as garantias críticas permanecem dentro de transações locais controladas pelo serviço.

### Trade-offs

- exige armazenamento de `ProcessedEvent`;
- exige política de retenção ou limpeza para registros de eventos processados;
- a persistência precisa suportar restrições de unicidade e operações atômicas de `create-or-increment`;
- concorrência e redelivery precisam ser cobertos por testes de integração;
- falhas de processamento precisam ser diferenciadas de eventos já processados;
- a implementação fica dependente das garantias transacionais e de concorrência fornecidas pelo banco de dados escolhido.

## Alternativas consideradas

### Verificação prévia do EventId

Consultar se um `EventId` já foi processado antes de registrá-lo não é suficiente.

Dois workers poderiam realizar a consulta simultaneamente, ambos observarem a ausência do registro e continuarem o processamento.

A unicidade no banco é utilizada como mecanismo definitivo de arbitragem.

### Consultar antes de criar DailyConsolidation

Consultar previamente se a projeção diária existe e posteriormente escolher entre `INSERT` e `UPDATE` também introduziria uma condição de corrida.

A operação atômica de `create-or-increment` evita essa janela.

### Read-modify-write do saldo

Calcular o novo saldo na aplicação após ler o valor atual permitiria `lost update` durante processamento concorrente.

A alternativa foi rejeitada em favor da alteração atômica do valor persistido.

## Decisões relacionadas

- ADR-001 — Comunicação assíncrona orientada a eventos
- ADR-002 — Apache Kafka como mecanismo de mensageria
- ADR-003 — Transactional Outbox para publicação de eventos
- `docs/architecture/reliability-and-processing-semantics.md` — detalhamento dos cenários de falha, redelivery, idempotência e concorrência
