# Architecture Blueprint

> **Cash Flow Control — Software Architecture Challenge**

## 1. Objetivo

Definir a arquitetura da solução para controle de fluxo de caixa diário, contemplando o registro de lançamentos de débito e crédito e a disponibilização do saldo diário consolidado.

A arquitetura deve atender aos requisitos de **disponibilidade, resiliência, escalabilidade, segurança, desempenho e observabilidade** definidos para a solução.

O registro de lançamentos e a consolidação serão tratados como responsabilidades independentes.

> **Premissa principal:** a indisponibilidade do processo de consolidação não poderá impedir o registro de novos lançamentos.

---

## 2. Escopo

A solução será composta por dois serviços de negócio.

### Launch Service

Responsável pelo recebimento, validação e persistência dos lançamentos de débito e crédito.

### Consolidation Service

Responsável pelo processamento dos lançamentos e disponibilização do saldo diário consolidado.

A comunicação necessária para atualização do consolidado será **assíncrona**, eliminando dependência de disponibilidade entre os dois serviços.

---

## 3. Premissas Arquiteturais

### 3.1. Durabilidade dos lançamentos

Um lançamento somente será considerado aceito após sua persistência durável.

Após confirmado, o lançamento não poderá ser perdido em decorrência de indisponibilidade do serviço de consolidação ou do mecanismo de mensageria.

### 3.2. Desacoplamento

O `Launch Service` não possuirá dependência síncrona do `Consolidation Service`.

Após a efetivação do lançamento, sua propagação ocorrerá através de mensageria assíncrona.

A indisponibilidade temporária do `Consolidation Service` poderá gerar atraso na atualização do saldo, mas não indisponibilidade no registro de lançamentos.

### 3.3. Consistência

Será adotado modelo de **consistência eventual** entre lançamentos e consolidado.

Durante falhas ou backlog de processamento, o consolidado poderá permanecer temporariamente defasado.

Após a recuperação do processamento, deverá convergir para o estado correspondente aos lançamentos efetivados.

### 3.4. Garantia de publicação

A confirmação de um lançamento não poderá criar uma janela de perda entre sua persistência e a publicação do respectivo evento.

A persistência do lançamento e da intenção de publicação deverão fazer parte da mesma unidade transacional.

A implementação desta garantia será definida através de **Architecture Decision Record (ADR)**.

### 3.5. Entrega e idempotência

O fluxo deverá suportar redelivery de mensagens.

A mensageria deverá utilizar os mecanismos nativos disponíveis para reduzir
duplicidades durante a publicação, incluindo publicação idempotente quando
suportada pela tecnologia adotada.

Considerando que o processamento envolve uma transação em datastore externo
ao mecanismo de mensageria, a solução adotará semântica **at-least-once**
fim a fim, com consumidores idempotentes.

O recebimento repetido de um mesmo evento não poderá provocar duplicidade
no saldo consolidado.

Não será assumida garantia distribuída de `exactly-once` entre o mecanismo
de mensageria e a persistência do `Consolidation Service`.

### 3.6. Ordenação

A regra atual de consolidação não depende da ordem global dos lançamentos.

Não será introduzida serialização global exclusivamente para garantia de ordenação.

Esta premissa deverá ser revista caso futuras regras de negócio passem a depender da sequência dos eventos.

---

## 4. Consolidação

O saldo diário será mantido como uma **projeção materializada** dos lançamentos.

Cada evento processado atualizará incrementalmente o consolidado correspondente.

A consulta do saldo diário utilizará essa projeção e não realizará o recálculo integral dos lançamentos sob demanda.

O processamento deverá garantir:

- atomicidade;
- idempotência;
- recuperação após falhas;
- execução concorrente;
- escala horizontal.

A solução não deverá depender de locks distribuídos ou de uma única instância do serviço para garantir consistência.

---

## 5. Escalabilidade

Os componentes de aplicação deverão ser **stateless sempre que possível**, permitindo escala horizontal.

O serviço de consolidado deverá suportar o pico especificado de:

> **50 requisições por segundo, com tolerância máxima de 5% de perda.**

Esse requisito será validado através de teste automatizado de carga.

As APIs deverão permitir escala horizontal caso a demanda justifique sua utilização.

Os workers responsáveis pelo processamento assíncrono também deverão permitir escala horizontal.

A demonstração prática de elasticidade será concentrada no processamento dos workers.

As métricas e limites utilizados para autoscaling serão definidos na arquitetura de deployment.

---

## 6. Resiliência

A solução deverá continuar registrando lançamentos durante indisponibilidade do `Consolidation Service`.

Eventos ainda não processados deverão permanecer recuperáveis e ser processados após o restabelecimento do serviço.

A indisponibilidade temporária do mecanismo de mensageria também não deverá resultar em perda de lançamentos já aceitos.

Após sua recuperação, os eventos pendentes deverão ser publicados e o consolidado deverá convergir novamente.

Esses comportamentos serão validados através de **testes controlados de falha**.

---

## 7. Observabilidade

Todos os serviços deverão produzir telemetria suficiente para diagnóstico operacional e acompanhamento do fluxo distribuído.

A solução deverá disponibilizar:

- Structured Logs;
- Metrics;
- Distributed Tracing;
- Health Checks;
- Readiness Probes;
- Liveness Probes.

Deverá ser possível correlacionar uma operação desde o recebimento do lançamento até seu processamento pelo `Consolidation Service`.

A implementação da stack de observabilidade será registrada através de ADR.

---

## 8. Segurança

A comunicação entre os componentes deverá utilizar transporte seguro.

Autenticação e autorização serão aplicadas aos pontos de entrada da solução conforme a responsabilidade de cada serviço.

Credenciais, connection strings e secrets não poderão ser mantidos no código-fonte.

Os mecanismos adotados deverão ser proporcionais ao escopo do desafio e preservar a possibilidade de evolução para um ambiente produtivo.

---

## 9. Estratégia de Validação

A solução possuirá diferentes níveis de validação.

### Unit Tests

Validação das regras de domínio e componentes isolados.

### Integration Tests

Validação das integrações críticas, incluindo persistência, mensageria e idempotência.

### Load Tests

O cenário nominal deverá sustentar **50 req/s** no serviço de consolidado até a interrupção manual do teste.

Durante sua execução deverão ser observados, no mínimo:

- throughput;
- quantidade de requisições;
- taxa de sucesso;
- taxa de erro;
- latência.

### Resilience Tests

Deverão ser validados os seguintes cenários:

1. indisponibilidade do `Consolidation Service`;
2. indisponibilidade da mensageria;
3. recuperação dos componentes;
4. processamento do backlog;
5. redelivery sem duplicidade financeira;
6. convergência do saldo consolidado;
7. escala horizontal dos workers.

---

## 10. Execução e Deployment

A solução possuirá dois modos de execução.

### Local Environment

O ambiente local será disponibilizado através de **Docker Compose**, permitindo executar a solução completa com o menor número possível de dependências externas.

A experiência esperada será próxima de:

```bash
docker compose up
