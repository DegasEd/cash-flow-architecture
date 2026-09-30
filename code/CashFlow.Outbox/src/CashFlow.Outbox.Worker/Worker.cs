using Confluent.Kafka;
using CashFlow.Outbox.Core;
using CashFlow.Outbox.Core.Interfaces;
using CashFlow.Outbox.Repository.Interfaces;
using CashFlow.Outbox.Worker;

namespace CashFlow.Outbox.Worker;

public class Worker : BackgroundService
{
    private readonly IOutboxPublisherService _outboxPublisherService;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IOutboxPublisherService outboxPublisherService,
        ILogger<Worker> logger)
    {
        _outboxPublisherService = outboxPublisherService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _outboxPublisherService.PublishPendingAsync(
                    stoppingToken);
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
                    "Error while publishing pending outbox events.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                stoppingToken);
        }
    }
}