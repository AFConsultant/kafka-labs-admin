using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

app.Run();

public class KafkaConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaConsumerService> _logger;

    public KafkaConsumerService(IServiceScopeFactory scopeFactory, ILogger<KafkaConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "kafka:9092",
            GroupId = "citibike-dotnet-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        const string topic = "bike_trips_raw_csv";

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe(topic);
        _logger.LogInformation("Kafka Consumer started. Subscribed to '{Topic}' topic.", topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    var csvLine = consumeResult.Message.Value;

                    if (string.IsNullOrWhiteSpace(csvLine)) continue;

                    _logger.LogInformation("Consumed line \n" + csvLine);
                }
                catch (ConsumeException e)
                {
                    _logger.LogError("Error consuming from Kafka: {Reason}", e.Error.Reason);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka consumer is shutting down.");
        }
        finally
        {
            consumer.Close();
        }
    }

}
