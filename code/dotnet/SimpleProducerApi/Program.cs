using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;

var producerConfig = new ProducerConfig
{
    BootstrapServers = "kafka:9092"
};


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IProducer<Null, string>>(_ =>
    new ProducerBuilder<Null, string>(producerConfig).Build());

var app = builder.Build();

app.UseHttpsRedirection();

const string filePath = "/workspaces/kafka-labs-admin/data/201306-citibike-tripdata_1_6K.csv";
const string topicName = "bike_trips_raw_csv";

app.MapPost("/trip", ([FromServices] IProducer<Null, string> producer) => {
    
    if (!File.Exists(filePath))
    {
        return Results.NotFound($"Le fichier n'a pas été trouvé à l'emplacement : {filePath}");
    }

    _ = Task.Run(async () => {
        try
        {            
            bool firstLine = true;
            await foreach (var line in File.ReadLinesAsync(filePath))
            {
                if (firstLine)
                {
                    firstLine = false;
                    continue;
                }
                var message = new Message<Null, string> { Value = line };
                try
                {
                    await producer.ProduceAsync(topicName, message);
                    Console.WriteLine($"Ligne envoyée à Kafka : {line}");
                }
                catch (ProduceException<Null, string> e)
                {
                    Console.WriteLine($"Échec de l'envoi du message : {message.Value} | Raison : {e.Error.Reason}");
                }
                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Une erreur est survenue lors de la lecture du fichier : {ex.Message}");
        }
    });
    return Results.Accepted();
});

app.Run();