using Confluent.Kafka;
using CashFlow.Outbox.Core;
using CashFlow.Outbox.Core.Interfaces;
using CashFlow.Outbox.Repository;
using CashFlow.Outbox.Repository.Interfaces;
using CashFlow.Outbox.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var connectionString =
    builder.Configuration.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException(
        "Connection string 'PostgreSql' was not configured.");

var kafkaBootstrapServers =
    builder.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException(
        "Kafka bootstrap servers were not configured.");

builder.Services.AddSingleton<IOutboxRepository>(_ =>
    new OutboxRepository(connectionString));

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers,
        Acks = Acks.All
    };

    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IOutboxPublisherService, OutboxPublisherService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();