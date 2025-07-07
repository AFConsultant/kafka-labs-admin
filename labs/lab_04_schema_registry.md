# Lab 04 - Using the Schema Registry

## Goal
In our previous labs, we sent raw strings as messages. This is simple, but fragile. If a producer changes the format (e.g., adds a field, changes a date format), the consumer might break without warning. This is a common and difficult problem in data integration.

The Confluent Schema Registry solves this problem by acting as a central repository for your data schemas. It allows producers and consumers to agree on a data contract. By using a format like Apache Avro, we can send strongly-typed, compact binary data and ensure that producers and consumers remain compatible even as schemas evolve.

Our goal in this lab is to refactor our producer and consumer to:
* Define a formal Avro schema for our bike trip data.
* Generate a class from this Schema.
* Use this class in new producer and consumer projects.

## Prerequisites
Before you begin, please ensure you have the following:

* The Docker environment from Lab 01 is up and running (docker-compose up -d).
* The Schema Registry service is included in the docker-compose.yml file and runs at http://localhost:8081.

## Your Mission: Integrate with Schema Registry

### Define the Avro Schema

First, we need to define the structure of our data. Create a new file named bike_trip.avsc. This file will contain the Avro schema definition in JSON format. We will place this file in a location accessible to both our producer and consumer projects.

```json
{
  "type": "record",
  "name": "CitiBikeTrip",
  "namespace": "com.citibike",
  "fields": [
    {
      "name": "TripDuration",
      "type": "int"
    },
    {
      "name": "StartTime",
      "type": {
        "type": "long",
        "logicalType": "timestamp-millis"
      }
    },
    {
      "name": "StopTime",
      "type": {
        "type": "long",
        "logicalType": "timestamp-millis"
      }
    },
    {
      "name": "StartStationId",
      "type": "int"
    },
    {
      "name": "StartStationName",
      "type": [
        "null",
        "string"
      ],
      "default": null
    },
    {
      "name": "StartStationLatitude",
      "type": "double"
    },
    {
      "name": "StartStationLongitude",
      "type": "double"
    },
    {
      "name": "EndStationId",
      "type": [
        "null",
        "int"
      ],
      "default": null
    },
    {
      "name": "EndStationName",
      "type": [
        "null",
        "string"
      ],
      "default": null
    },
    {
      "name": "EndStationLatitude",
      "type": [
        "null",
        "double"
      ],
      "default": null
    },
    {
      "name": "EndStationLongitude",
      "type": [
        "null",
        "double"
      ],
      "default": null
    },
    {
      "name": "BikeId",
      "type": "int"
    },
    {
      "name": "UserType",
      "type": [
        "null",
        "string"
      ],
      "default": null
    },
    {
      "name": "BirthYear",
      "type": [
        "null",
        "string"
      ],
      "default": null
    },
    {
      "name": "Gender",
      "type": "int"
    }
  ]
}
```

### Generate the classes 

A class needs to be generated from this schema.

#### In .NET

Use Apache Avro Tools:

```sh
dotnet tool install --global Apache.Avro.Tools
avrogen -s CitiBikeTrip.avsc Generated
```

This will generate sources inside the `Generated` folder.

#### In Java

TODO

### Copy and refactor the Producer

The producer's job is to read the CSV file, but instead of sending each line as a raw string, it must:

* Parse the CSV line into its individual fields.
* Create an instance of an object representing the trip (using the generated class or a generic class).
* Serialize this object using the KafkaAvroSerializer.
* Send the data to the `bike_trips` topic.

### Copy and refactor the Consumer

The consumer's job is to:

* Subscribe to a new topic, let's call it `bike_trips`.
* Use the KafkaAvroDeserializer to automatically convert the incoming binary Avro data back into a usable object.
* Print the fields from the deserialized object to the console.

### Refactoring tips - .NET
* Add the Confluent.SchemaRegistry and Confluent.SchemaRegistry.Serdes.Avro packages to your producer and consumer projects.
* Create a SchemaRegistryConfig pointing the Url to http://localhost:8081. Instantiate a CachedSchemaRegistryClient using this configuration.
* When building your producer, use the .SetValueSerializer() method and provide it with a new AvroSerializer<T> (where T is the generated class).
* When building your consumer, use the .SetValueDeserializer() method and provide it with a new AvroDeserializer<T>, (where T is the generated class).

### Run and Verify
* Start your refactored producer. It will read the CSV and send Avro-encoded messages to the bike_trips_avro topic.
* The first time it runs, it will contact the Schema Registry on http://localhost:8081, upload the schema, and cache the schema ID it gets back.
* Start your refactored consumer. It will read the messages. When it sees a message, it will extract the schema ID, request the full schema from the Schema Registry (and then cache it), and use it to deserialize the binary data into a usable object.
* Verify that your consumer prints the structured data from the object, not just a raw string. You can also inspect the registered schemas using the AKHQ web interface.