using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.AspNetCore.Mvc;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using com.citibike;
using CsvIndex = CsvHelper.Configuration.Attributes.IndexAttribute;

var schemaRegistryConfig = new SchemaRegistryConfig
{
    Url = "http://schema-registry:8081"
};

var producerConfig = new ProducerConfig
{
    BootstrapServers = "kafka:9092"
};


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ISchemaRegistryClient>(_ =>
    new CachedSchemaRegistryClient(schemaRegistryConfig));
builder.Services.AddSingleton<IProducer<Null, CitiBikeTrip>>(sp =>
{
    var schemaRegistry = sp.GetRequiredService<ISchemaRegistryClient>();

    var avroSerializerConfig = new AvroSerializerConfig
    {
        AutoRegisterSchemas = false
    };

    var avroSerializer = new AvroSerializer<CitiBikeTrip>(schemaRegistry, avroSerializerConfig);

    return new ProducerBuilder<Null, CitiBikeTrip>(producerConfig)
        .SetValueSerializer(avroSerializer)
        .Build();
});

var app = builder.Build();

app.UseHttpsRedirection();

var filePath = Environment.GetEnvironmentVariable("CSV_FILE_PATH") ?? "/workspaces/kafka-labs/data/201306-citibike-tripdata_1_6K.csv";
const string topicName = "bike_trips";

app.MapPost("/trip", ([FromServices] IProducer<Null, CitiBikeTrip> producer) =>
{

    if (!File.Exists(filePath))
    {
        return Results.NotFound($"Le fichier n'a pas été trouvé à l'emplacement : {filePath}");
    }

    _ = Task.Run(async () =>
    {
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
                await SendAvroMessage(producer, line);
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


async Task SendAvroMessage(IProducer<Null, CitiBikeTrip> producer, string line)
{
    var citibike = ParseCsvLine(line);
    var message = new Message<Null, CitiBikeTrip> { Value = citibike };
    try
    {
        await producer.ProduceAsync(topicName, message);
        Console.WriteLine($"Ligne envoyée à Kafka : {line}");
    }
    catch (ProduceException<Null, CitiBikeTrip> e)
    {
        Console.WriteLine(e.ToString());
        Console.WriteLine($"Échec de l'envoi du message : {message.Value} | Raison : {e.Error.Reason}");
    }
}

app.Run();

CitiBikeTrip ParseCsvLine(string csvLine)
{
    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
    };

    using var reader = new StringReader(csvLine);
    using var csv = new CsvReader(reader, config);

    var numberStyle = NumberStyles.Float;
    csv.Context.TypeConverterOptionsCache.GetOptions<int?>().NumberStyles = numberStyle;
    csv.Context.TypeConverterOptionsCache.GetOptions<int>().NumberStyles = numberStyle;

    if (csv.Read())
    {
        var record = csv.GetRecord<CitiBikeTripCsv>() ?? throw new Exception("Failed to process csv record " + csvLine);
        return record.Convert();
    }
    throw new Exception("Failed to process csv record " + csvLine);
}

public class CitiBikeTripCsv
{
    [CsvIndex(0)]
    public int TripDuration { get; set; }

    [CsvIndex(1)]
    public DateTime StartTime { get; set; }

    [CsvIndex(2)]
    public DateTime StopTime { get; set; }

    [CsvIndex(3)]
    public int StartStationId { get; set; }

    [CsvIndex(4)]
    public string? StartStationName { get; set; }

    [CsvIndex(5)]
    public double StartStationLatitude { get; set; }

    [CsvIndex(6)]
    public double StartStationLongitude { get; set; }

    [CsvIndex(7)]
    public int? EndStationId { get; set; }

    [CsvIndex(8)]
    public string? EndStationName { get; set; }

    [CsvIndex(9)]
    public double? EndStationLatitude { get; set; }

    [CsvIndex(10)]
    public double? EndStationLongitude { get; set; }

    [CsvIndex(11)]
    public int BikeId { get; set; }

    [CsvIndex(12)]
    public string? UserType { get; set; }

    [CsvIndex(13)]
    public string? BirthYear { get; set; }

    [CsvIndex(14)]
    public int Gender { get; set; }

    public CitiBikeTrip Convert()
    {
        CitiBikeTrip citiBikeTrip = new CitiBikeTrip();
        citiBikeTrip.TripDuration = this.TripDuration;
        citiBikeTrip.StartTime = this.StartTime;
        citiBikeTrip.StopTime = this.StopTime;
        citiBikeTrip.StartStationId = this.StartStationId;
        citiBikeTrip.StartStationName = this.StartStationName;
        citiBikeTrip.StartStationLatitude = this.StartStationLatitude;
        citiBikeTrip.EndStationId = this.EndStationId;
        citiBikeTrip.EndStationName = this.EndStationName;
        citiBikeTrip.EndStationLatitude = this.EndStationLatitude;
        citiBikeTrip.EndStationLongitude = this.EndStationLongitude;
        citiBikeTrip.BikeId = this.BikeId;
        citiBikeTrip.UserType = this.UserType;
        citiBikeTrip.BirthYear = this.BirthYear;
        citiBikeTrip.Gender = this.Gender;
        
        return citiBikeTrip;
    }
}

