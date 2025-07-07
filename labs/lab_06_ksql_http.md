# Preparation

ksqlDB should be running.
Bike trips data should be sent to the `bike_trips` topic. Keep the producer running.
Stations data should be sent to the `station_infos_raw_csv` topic.

# Http queries

## Show streams

```sh
curl -X POST localhost:8088/ksql \
     -H "Content-Type: application/vnd.ksql.v1+json" \
     -d '{"ksql":"SHOW STREAMS;"}'
```

## Create stations stream

```sh
curl -X POST "http://localhost:8088/ksql" \
     -H "Content-Type: application/vnd.ksql.v1+json; charset=utf-8" \
     -d '{
         "ksql": "CREATE TABLE station_details (station_id VARCHAR PRIMARY KEY, station_id_dup VARCHAR, station_name VARCHAR, latitude DOUBLE, longitude DOUBLE, neighborhood VARCHAR) WITH (KAFKA_TOPIC='\''station_infos_raw_csv'\'', VALUE_FORMAT='\''DELIMITED'\'');",
         "streamsProperties": {}
     }'
```

## Make the stations queryables

```sh
curl -X POST "http://localhost:8088/ksql" \
     -H "Content-Type: application/vnd.ksql.v1+json; charset=utf-8" \
     -d @- <<-'EOF'
{
    "ksql": "CREATE TABLE QUERYABLE_STATION_DETAILS AS SELECT * FROM STATION_DETAILS;"
}
EOF
```

## Get all stations

```sh
curl -X "POST" "http://localhost:8088/query-stream" \
     -H "Content-Type: application/vnd.ksql.v1+json; charset=utf-8" \
     -d @- <<-'EOF'
{
  "sql": "SELECT * FROM QUERYABLE_STATION_DETAILS;",
  "streamsProperties": {
    "ksql.streams.auto.offset.reset": "earliest"
  }
}
EOF
```

## Get a single station

```sh
curl -X "POST" "http://localhost:8088/query-stream" \
     -H "Content-Type: application/vnd.ksql.v1+json; charset=utf-8" \
     -d @- <<-'EOF'
{
  "sql": "SELECT STATION_NAME FROM QUERYABLE_STATION_DETAILS WHERE station_id = '475';"
}
EOF
```

## Get a continuous stream of results

Recreate the `bike_trips` stream (see lab 05).
Query it using continuous queries.

```sh
curl -X "POST" "http://localhost:8088/query-stream" \
     -H "Content-Type: application/vnd.ksql.v1+json; charset=utf-8" \
     -d @- <<-'EOF'
{
  "sql": "SELECT * FROM TRIPS EMIT CHANGES;",
  "streamsProperties": {
    "ksql.streams.auto.offset.reset": "earliest"
  }
}
EOF
```

## Use the REST api in python code

Check the script in `./code/python/show_trips_stream.py`.
Run it.
```
python3 show_trips_stream.py
```
See the results streaming in.