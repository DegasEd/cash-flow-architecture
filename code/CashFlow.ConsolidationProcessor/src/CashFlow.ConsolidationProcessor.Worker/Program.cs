using Confluent.Kafka;
using CashFlow.ConsolidationProcessor.Core;
using CashFlow.ConsolidationProcessor.Core.Interfaces;
using CashFlow.ConsolidationProcessor.Repository;
using CashFlow.ConsolidationProcessor.Repository.Interfaces;
using CashFlow.ConsolidationProcessor.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException(
        "Connection string 'PostgreSql' was not configured.");

var kafkaBootstrapServers =
    builder.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException(
        "Kafka bootstrap servers were not configured.");

var kafkaGroupId =
    builder.Configuration["Kafka:GroupId"]
    ?? throw new InvalidOperationException(
        "Kafka consumer group was not configured.");

builder.Services.AddSingleton<IConsolidationRepository>(_ =>
    new ConsolidationRepository(connectionString));

builder.Services.AddSingleton<IConsolidationService, ConsolidationService>();

builder.Services.AddSingleton<IConsumer<string, string>>(_ =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = kafkaBootstrapServers,
        GroupId = kafkaGroupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
    };

    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();