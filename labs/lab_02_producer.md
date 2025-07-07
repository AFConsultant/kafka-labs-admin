# Lab 02 - Create a Kafka Producer

## Goal

In [Lab 01](./lab_01_fundamentals.md), we used command-line tools to interact with Kafka. While useful for ad-hoc tasks and debugging, most real-world applications send data to Kafka programmatically.

Our goal in this lab is to build our first client application: a Kafka Producer. This application will read the Citi Bike trip data from the CSV file and send each trip as a separate message to the bike_trips_raw_csv topic we created earlier.

This lab currently provides guidance for Java and .NET developers.

## Prerequisites
Before you begin, please ensure you have the following:

* The Docker environment from Lab 01 is up and running (`docker-compose up -d`).
* You have the appropriate SDK for your chosen language installed: For java, JDK 21 and Gradle, For C#, .NET 9 SDK (or newer).

## Your Mission: Build the Producer

The core task is to create a simple application that performs the following steps:

* Connects to our local Kafka cluster.
* Opens the /workspaces/kafka-labs/data/201306-citibike-tripdata_1_6K.csv file.
* Reads the file line by line, skipping the first line (which is the header row).
* Sends each line of data as a string to the bike_trips_raw_csv topic.
* Wait 1 second between each message.
* Properly closes the connection to Kafka.

## For Java Developers (using Gradle)

### Project Setup
* Navigate to the /workspaces/kafka-labs/solution/java directory.
* Create a new directory for your producer application (e.g., bike-trip-producer).
* Inside that new directory, initialize a new Gradle project. The `gradle init` command will be your starting point. Choose to create a basic "application".

### Add Dependencies
* Your application needs the official Kafka client library to communicate with the cluster.
* Find your build.gradle.kts (or build.gradle) file and add the kafka-clients library as a dependency. You can find the latest version on Maven Central.

### Producer Configuration
* Your producer needs to know where to find the Kafka cluster. This is the most critical configuration. The most important property to set is `bootstrap.servers`. Since your application will run on your host machine (outside Docker), and you added `kafka 127.0.0.1` to your `/etc/hosts` it should point to `kafka:9092`.
* You will also need to tell Kafka how to serialize your message's key and value. Since we are sending simple strings, you'll need to configure a key.serializer and value.serializer to use the StringSerializer.

### Implement the Producer Logic
* In your main application class, instantiate KafkaProducer, passing it your configuration properties. 
* This about your Producer types. What will you send as key ? As value ?
* Implement the logic to read the CSV file line by line, remembering to handle the header.
* For each data row, you need to create a ProducerRecord. A ProducerRecord is a wrapper for the message you want to send. It contains the target topic, and optionally a key and a value. For now, the value will be the CSV line, and you can leave the key as null.
* Use the producer's .send() method to dispatch the ProducerRecord. This method is asynchronous; it returns a Future that you can use to get information about the sent message. Make sure each message is sent sucessfully to Kafka. Is this way of doing it efficient ?
* Wait 1 second before sending the next message.
* Crucially, once you have sent all the lines from the file, you must call the producer's .close() method. This will block until all previously sent messages have been delivered and will release the resources.

### Run and Verify

* Run your application using gradle run.
* Verify your success by using the kafka-console-consumer from Lab 01 or by checking the topic's messages in the AKHQ interface at http://localhost:8085.

## For .NET Developers

### Project Setup

* Navigate to the /workspaces/kafka-labs/solution/dotnet directory.
* Create a new directory for your producer application (e.g., BikeTripProducer).
* Inside that new directory, initialize a new .NET console application using the dotnet new console command.

### Add Dependencies

* Your application needs a Kafka client library. The most widely used and officially supported one for .NET is Confluent.Kafka.
* Use the dotnet add package Confluent.Kafka command to add this dependency to your project.

### Producer Configuration

* Your producer needs to know where to find the Kafka cluster.
* You will create a ProducerConfig object in your code. The most important property to set is BootstrapServers. Since your application will run on your host machine (outside Docker), and you added `kafka 127.0.0.1` to your `/etc/hosts` it should point to `kafka:9092`.
* Unlike the Java client, the Confluent client's generic IProducer<TKey, TValue> handles serialization implicitly, so you don't need to configure serializers in the config object itself.

### Implement the Producer Logic

* In your Program.cs file, use a ProducerBuilder to construct your producer instance. You will create an IProducer<Null, string>.
* Implement the logic to read the CSV file line by line. You can use File.ReadLines() and the LINQ .Skip(1) method to easily bypass the header.
* For each data row, you need to create a Message<Null, string>. The Value property will be the CSV line.
* Use the producer's .ProduceAsync() method to send your message to the bike_trips_raw_csv topic. This method is asynchronous.
* Crucially, the producer instance should be created within a using block to ensure it is disposed of correctly. Disposing the producer will flush any outstanding messages and close the connection.

### Run and Verify

* Run your application using dotnet run.
* Verify your success by using the kafka-console-consumer from Lab 01 or by checking the topic's messages in the AKHQ interface at http://localhost:8085.

## Helpful Resources
### Java:

* Apache Kafka Producer Documentation
* Gradle Docs: Building Java Applications

### .NET:

* Confluent.Kafka .NET Client on NuGet
* Confluent .NET Client GitHub (with examples)
 .NET Docs: dotnet new console

Good luck! In the next lab, we will explore how to create a consumer.