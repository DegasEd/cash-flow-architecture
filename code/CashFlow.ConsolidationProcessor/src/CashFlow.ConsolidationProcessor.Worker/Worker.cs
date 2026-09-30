using Confluent.Kafka;
using CashFlow.ConsolidationProcessor.Core.Interfaces;

namespace CashFlow.ConsolidationProcessor.Worker;

public class Worker : BackgroundService
{
    private const string Topic = "entry-events";

    private readonly IConsumer<string, string> _consumer;
    private readonly IConsolidationService _consolidationService;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IConsumer<string, string> consumer,
        IConsolidationService consolidationService,
        ILogger<Worker> logger)
    {
        _consumer = consumer;
        _consolidationService = consolidationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _consumer.Subscribe(Topic);

        _logger.LogInformation(
            "Consolidation Processor started. Consuming topic {Topic}.",
            Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> message;

                try
                {
                    message = _consumer.Consume(stoppingToken);
                }
                catch (ConsumeException exception)
                {
                    _logger.LogError(
                        exception,
                        "Error while consuming Kafka message.");

                    continue;
                }

                try
                {
                    var processed =
                        await _consolidationService.ProcessAsync(
                            message.Message.Value,
                            stoppingToken);

                    _consumer.Commit(message);

                    _logger.LogInformation(
                        processed
                            ? "Event processed successfully. Partition: {Partition}, Offset: {Offset}."
                            : "Duplicate event ignored. Partition: {Partition}, Offset: {Offset}.",
                        message.Partition.Value,
                        message.Offset.Value);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Error while processing Kafka message. Partition: {Partition}, Offset: {Offset}.",
                        message.Partition.Value,
                        message.Offset.Value);
                }
            }
        }
        finally
        {
            _consumer.Close();
        }
    }
}