using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using com.citibike;

var builder = WebApplication.CreateBuilder(args);

var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var dbPath = Path.Combine(userProfile, "citibike.db");

// The database file will be created at /home/vscode/citibike.db
builder.Services.AddDbContext<TripDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/trips", async (TripDbContext db, [FromQuery] int page = 1, [FromQuery] int pageSize = 100) =>
{
    pageSize = Math.Clamp(pageSize, 1, 1000);

    var trips = await db.Trips
        .AsNoTracking()
        .OrderBy(t => t.StartTime)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Results.Ok(trips);
});

app.MapGet("/trip/{tripId:int}", async (int tripId, TripDbContext db) =>
{
    var trip = await db.Trips.FindAsync(tripId);

    if (trip is null)
    {
        return Results.NotFound($"Trip with ID {tripId} not found.");
    }

    return Results.Ok(trip);
});

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TripDbContext>();
    dbContext.Database.EnsureCreated();
}

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
        return Task.Run(async () => await ConsumeLoop(stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "kafka:9092",
            GroupId = "citibike-dotnet-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        var schemaRegistryConfig = new SchemaRegistryConfig
        {
            Url = "http://schema-registry:8081"
        };
        const string topic = "bike_trips";

        using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);
        var avroDeserializer = new AvroDeserializer<CitiBikeTrip>(schemaRegistry).AsSyncOverAsync();
        using var consumer = new ConsumerBuilder<Ignore, CitiBikeTrip>(config)
            .SetValueDeserializer(avroDeserializer)
            .Build();
        consumer.Subscribe(topic);
        _logger.LogInformation("Kafka Consumer started. Subscribed to '{Topic}' topic.", topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    await ProcessRecord(consumeResult.Message.Value);
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

    private async Task ProcessRecord(CitiBikeTrip citiBikeTrip)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TripDbContext>();

        CitiBikeTripDao dao = CitiBikeTripDao.fromAvroMessage(citiBikeTrip);
        dbContext.Trips.Add(dao);
        await dbContext.SaveChangesAsync();
        _logger.LogInformation($"Successfully processed and saved trip with id: {dao.Id}");
    }
}

public class CitiBikeTripDao
{
    public int TripDuration { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime StopTime { get; set; }

    public int StartStationId { get; set; }

    public string? StartStationName { get; set; }

    public double StartStationLatitude { get; set; }

    public double StartStationLongitude { get; set; }

    public int? EndStationId { get; set; }

    public string? EndStationName { get; set; }

    public double? EndStationLatitude { get; set; }

    public double? EndStationLongitude { get; set; }

    public int BikeId { get; set; }

    public string? UserType { get; set; }

    public string? BirthYear { get; set; }

    public int Gender { get; set; }

    [Key]
    public int Id { get; set; }

    public static CitiBikeTripDao fromAvroMessage(CitiBikeTrip citiBikeTrip)
    {
        CitiBikeTripDao citiBikeTripDao = new CitiBikeTripDao();
        citiBikeTripDao.TripDuration = citiBikeTrip.TripDuration;
        citiBikeTripDao.StartTime = citiBikeTrip.StartTime;
        citiBikeTripDao.StopTime = citiBikeTrip.StopTime;
        citiBikeTripDao.StartStationId = citiBikeTrip.StartStationId;
        citiBikeTripDao.StartStationName = citiBikeTrip.StartStationName;
        citiBikeTripDao.StartStationLatitude = citiBikeTrip.StartStationLatitude;
        citiBikeTripDao.EndStationId = citiBikeTrip.EndStationId;
        citiBikeTripDao.EndStationName = citiBikeTrip.EndStationName;
        citiBikeTripDao.EndStationLatitude = citiBikeTrip.EndStationLatitude;
        citiBikeTripDao.EndStationLongitude = citiBikeTrip.EndStationLongitude;
        citiBikeTripDao.BikeId = citiBikeTrip.BikeId;
        citiBikeTripDao.UserType = citiBikeTrip.UserType;
        citiBikeTripDao.BirthYear = citiBikeTrip.BirthYear;
        citiBikeTripDao.Gender = citiBikeTrip.Gender;

        return citiBikeTripDao;
    }
}


/// <summary>
/// The Entity Framework database context. It represents the session with the
/// database and allows us to query and save data.
/// </summary>
public class TripDbContext : DbContext
{
    // Constructor that accepts DbContextOptions, allowing us to configure
    // it from Program.cs (e.g., to specify the database provider and connection string).
    public TripDbContext(DbContextOptions<TripDbContext> options) : base(options) { }

    // Represents the "Trips" table in the database.
    // Each row in the table will correspond to a CitiBikeTripDao object.
    public DbSet<CitiBikeTripDao> Trips { get; set; }
}
