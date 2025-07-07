# ksqlDB
## Connect to ksqlDB
```sh
ksql http://localhost:8088
```

## Create the bike trips stream
```sql
CREATE STREAM trips (
  "TripDuration" INT,
  "StartTime" BIGINT,
  "StopTime" BIGINT,
  "StartStationId" INT,
  "StartStationName" VARCHAR,
  "StartStationLatitude" DOUBLE,
  "StartStationLongitude" DOUBLE,
  "EndStationId" INT,
  "EndStationName" VARCHAR,
  "EndStationLatitude" DOUBLE,
  "EndStationLongitude" DOUBLE,
  "BikeId" INT,
  "UserType" VARCHAR,
  "BirthYear" VARCHAR,
  "Gender" INT
) WITH (
  KAFKA_TOPIC='bike_trips',
  VALUE_FORMAT='AVRO',
  TIMESTAMP='"StartTime"'
);
```
Note that we use the `StartTime` timestamp. 
Have a look at your data and check the start times dates.
Why do we need this timestamp ? Think about Event Time vs Processing Time.

Test that the stream was properly created using a select:
```sql
show streams;
select * from trips emit changes;
```
Press `ctrl + C` to cancel the query.

We see only the newest trips. Configure ksqlDB to show the previous trips :
```sql
SET 'auto.offset.reset'='earliest';
SELECT * FROM trips EMIT CHANGES;
```

## Filter the trips by duration
The given duration is expressed in seconds.
Filter only the trips that lasted more than 10 minutes.
Show the start and end station names for these long trips.
Send the result to a topic `long_trips`.
```sql
CREATE STREAM long_trips WITH (KAFKA_TOPIC='long_trips') AS
SELECT
    `StartStationName`,
    `EndStationName`,
    `TripDuration` / 60 AS DurationMinutes
FROM trips
WHERE `TripDuration` / 60 > 10;
```
Check the results.
```sql
SELECT * FROM long_trips EMIT CHANGES;
```

## Load the station details data
We are given additional details about the stations, including the neighborhood of the stations, in a CSV file: [stations.csv](/data/stations.csv).

This file shows stations like this :
```text
station id,station name,latitude,longitude,neighborhood
444,Broadway & W 24 St,40.7423543,-73.98915076,Flatiron District
406,Hicks St & Montague St,40.69512845,-73.99595065,Brooklyn Heights
```
Load the given stations raw csv data into the topic `station_infos_raw_csv`. Set the key of the station messages as the `station id`.

For this, we can use `kcat`.
```sh
tail -n +2 ./data/stations.csv | awk -F',' '{print $1 "\t" $0}' | kcat -b kafka:9092 -t station_infos_raw_csv -P -K $'\t' -X partitioner=murmur2_random
```
Check the data is properly loaded. 
```sql
SET 'auto.offset.reset'='earliest';
PRINT station_infos_raw_csv LIMIT 5;
```
Create a stream based on this data using the delimiter formatter.
``` sql
CREATE TABLE station_details (
    station_id VARCHAR PRIMARY KEY,
    station_id_dup VARCHAR,
    station_name VARCHAR,
    latitude DOUBLE,
    longitude DOUBLE,
    neighborhood VARCHAR
) WITH (
    KAFKA_TOPIC='station_infos_raw_csv',
    VALUE_FORMAT='DELIMITED'
);
```
Check the table works.
```sql
SELECT * FROM station_details EMIT CHANGES;
```
Since it is a table, what `cleanup.policy` does its backing topic have ?

## Join the trips and the station details

The goal is to enrich the trip data with the neighborhood.

First, think about the `co-partitionning`. Are the two topics co-partionned ? Why ? And why not ?
What would happen if we removed the `-X partitioner=murmur2_random` argument from the `kcat` command ?
Is something missing for the co-partitionning to work ?

### Add a key to the trips data

Derive a stream `trips_by_station`, which uses `trips` but adds a key.
What type will be the key of the stream ? Will it work with our `station_details` key ? What would we need to do ?
```sql
CREATE STREAM trips_by_station
  WITH (KAFKA_TOPIC='bike_trips_by_station', VALUE_FORMAT='AVRO')
AS SELECT *
   FROM trips
PARTITION BY CAST("StartStationId" AS STRING);
```
Check the stream is working.
```
PRINT bike_trips_by_station LIMIT 5;
```

### Implement the join
Implement the join between `trips_by_station` and `station_details`. The resulting stream should be `trips_enriched` and contain eveything from the trip plus the starting station. A type cast is required here.

Also, because it is a join, the join key needs to be in the select.
```sql
CREATE STREAM trips_enriched AS
SELECT
  s.station_id AS "StationId",
  t."TripDuration",
  t."StartTime",
  t."StopTime",
  t."StartStationId",
  t."StartStationName",
  s.neighborhood AS "StartStationNeighborhood",
  t."StartStationLatitude",
  t."StartStationLongitude",
  t."EndStationId",
  t."EndStationName",
  t."EndStationLatitude",
  t."EndStationLongitude",
  t."BikeId",
  t."UserType",
  t."BirthYear",
  t."Gender"
FROM trips_by_station t
LEFT JOIN station_details s 
ON CAST(t."StartStationId" AS STRING) = s.station_id;
```
Check the generated schema in the registry.
Check that the join is working - you shouldn't see any `NULL` value.
```sql
SELECT * FROM trips_enriched EMIT CHANGES;
```

## Data agregation using windowing
We would like to know the top 5 neighborhoods with the most departures during the last 60 minutes.
For this, we are going to use a time window to count our departures.
What time window are we going to use ? Are we going to use a tumbling or a sliding window ? Why ?

Create the table with the tumbling window.
```sql
CREATE TABLE neighborhood_departures_count 
AS SELECT 
    "StartStationNeighborhood",
    AS_VALUE("StartStationNeighborhood") AS neighborhood,
    FROM_UNIXTIME(WINDOWSTART) AS window_start,
    FROM_UNIXTIME(WINDOWEND) AS window_end,
    COUNT(*) AS departures_count
FROM trips_enriched
WINDOW HOPPING (SIZE 1 HOUR, ADVANCE BY 5 MINUTES)
GROUP BY "StartStationNeighborhood"
EMIT CHANGES;
```

## Send the data to Postgres using a connector

```sql
CREATE SINK CONNECTOR jdbc_sink_postgres_departures_01 WITH (
    'connector.class' = 'io.confluent.connect.jdbc.JdbcSinkConnector',
    'connection.url' = 'jdbc:postgresql://postgres:5432/',
    'connection.user' = 'postgres',                          
    'connection.password' = 'postgres',                      
    'topics' = 'NEIGHBORHOOD_DEPARTURES_COUNT',              
    'table.name.format' = 'neighborhood_departures_count',   
    'input.data.format' = 'AVRO',                            
    'key.converter' = 'org.apache.kafka.connect.storage.StringConverter',
    'value.converter' = 'io.confluent.connect.avro.AvroConverter',
    'value.converter.schema.registry.url' = 'http://schema-registry:8081',
    'pk.mode' = 'record_value',
    'pk.fields' = 'NEIGHBORHOOD,WINDOW_START',
    'insert.mode' = 'upsert',
    'auto.create' = 'true',
    'auto.evolve' = 'true',
    'db.timezone' = 'UTC'
);
```
Check the logs to see if there are any error in connect.
```sh
docker logs -f connect
```
Explore the resulting data in the postgres database.
```sh
docker exec -it postgres psql -U postgres -d postgres
```
Check the results:
```sql
SELECT "NEIGHBORHOOD",
       "WINDOW_START",
       "WINDOW_END",
       "DEPARTURES_COUNT"
FROM public.neighborhood_departures_count
WHERE "WINDOW_START" = '2013-06-01 10:00:00'
ORDER BY "DEPARTURES_COUNT" DESC
LIMIT 5;
```