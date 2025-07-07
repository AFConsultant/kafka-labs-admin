# Lab 03 - Create a Kafka Consumer

## Goal
In [Lab 02](./lab_02_producer.md), we successfully built a producer application to send Citi Bike trip data to our Kafka topic. Now it's time to consume that data.

The goal of this lab is to build our second client application: a Kafka Consumer. This application will connect to the Kafka cluster, subscribe to the bike_trips_raw_csv topic, and read the messages we sent in the previous lab, printing their contents to the screen.

This lab provides guidance for Java and .NET developers.

## Prerequisites
Before you begin, please ensure you have the following:

* The Docker environment from Lab 01 is up and running (docker-compose up -d).
* You have successfully run the producer from Lab 02 at least once to ensure there is data in the bike_trips_raw_csv topic.

## Your Mission: Build the Consumer
The core task is to create a simple application that performs the following steps:

* Connects to our local Kafka cluster.
* Subscribes to the bike_trips_raw_csv topic as part of a consumer group.
* Continuously polls Kafka for new records.
* For each record received, prints the message value, its offset in the partition, and its key to the console.
* Shuts down the consumer gracefully when killed.

## For Java Developers (using Gradle)

### Project Setup
* Navigate to the /workspaces/kafka-labs/solution/java directory.
* Create a new directory for your consumer application (e.g., bike-trip-consumer).
* Inside that new directory, initialize a new Gradle project using the `gradle init` command. Choose to create a basic "application".

###  Add Dependencies
* Your application needs the official Kafka client library to communicate with the cluster.
* Find your build.gradle.kts (or build.gradle) file and add the kafka-clients library as a dependency, just as you did for the producer.

### Consumer Configuration
Your consumer needs several key properties to function correctly. The most important are:
* bootstrap.servers: Point this to your Kafka cluster at kafka:9092.
* group.id: This is a crucial concept. It's a unique string that identifies the consumer group your application belongs to. All consumers with the same group.id will work together to consume a topic. Let's use bike-trip-reader for our group ID.
* key.deserializer and value.deserializer: The producer serialized the data into bytes. The consumer must deserialize it back. Since the producer used the StringSerializer, you must configure the consumer to use the corresponding StringDeserializer.
* auto.offset.reset: This tells the consumer where to start reading if it's a new group or if its last known offset is no longer valid. Set this to earliest so it always starts from the very beginning of the topic the first time it runs.

###  Implement the Consumer Logic
* In your main application class, instantiate KafkaConsumer, passing it your configuration properties.
* Subscribe your consumer to the desired topic by calling its .subscribe() method with a list containing bike_trips_raw_csv.
* Create a main consumption loop (e.g., a while(true) loop). This loop will be the heart of your application.
* Inside the loop, call the consumer's .poll() method. This method fetches records from Kafka. It's designed to wait for a specified duration if no records are available.
* The .poll() method returns a collection of ConsumerRecord objects. Iterate through this collection.
* For each ConsumerRecord, use its methods to get the message value(), offset(), and key() and print them to the console.
* Ensure you handle exceptions properly to allow for a graceful shutdown of the consumer, which involves calling the .close() method.

### Run and Verify
* Run your application using gradle run.
* You should see the CSV data from the topic printed to your console, along with the offset and key (which will likely be null) for each message. If you run your producer again while the consumer is running, you will see the new messages appear in real-time.

## For .NET Developers

### Project Setup
* Navigate to the /workspaces/kafka-labs/solution/dotnet directory.
* Create a new directory for your consumer application (e.g., BikeTripConsumer).
* Inside that new directory, initialize a new .NET console application using the dotnet new console command.

### Add Dependencies
* Add the Confluent.Kafka package to your project using the dotnet add package Confluent.Kafka command.

### Consumer Configuration
You will create a ConsumerConfig object in your code. The essential properties are:
* BootstrapServers: Point this to kafka:9092.
* GroupId: This string identifies your consumer group. All consumers in a group coordinate to avoid reading the same message. Use a name like bike-trip-reader.
* AutoOffsetReset: This determines where to begin consuming from if there's no stored offset for your group. Set this to AutoOffsetReset.Earliest to ensure you read all the messages currently in the topic on the first run.

### Implement the Consumer Logic
* In your Program.cs file, use a ConsumerBuilder to construct your consumer instance. You will be building an IConsumer<Null, string>.
* The consumer instance should be created within a using block to ensure its resources are properly managed and it is closed correctly on exit.
* Call the .Subscribe() method on your consumer instance, passing it the bike_trips_raw_csv topic name.
* Create a consumption loop. A common pattern is to use a CancellationTokenSource and a while loop that runs as long as cancellation has not been requested.
* Inside the loop, call the consumer's .Consume() method. This method will block until a message is received or the cancellation token is triggered.
* The .Consume() method returns a ConsumeResult object. From this object, you can access the message's Offset, Message.Key, and Message.Value. Print these details to the console.
* Set up a Console.CancelKeyPress event handler to trigger the CancellationTokenSource, allowing for a clean shutdown when you press Ctrl+C.

### Run and Verify
* Run your application using dotnet run.
* The console will display the messages from the bike_trips_raw_csv topic, including their offset and key. The consumer will continue running, waiting for new messages. You can run the producer from Lab 02 again to see them appear.

## Rewinding Your Consumer
* Your consumer application keeps track of what it has read using its group.id. What if you want to re-process all the messages from the beginning? You can't just restart the application, because Kafka remembers the group's last read position (the "offset").
* You can manually manage a consumer group's offset using the kafka-consumer-groups.sh command-line tool, which is available inside the kafka Docker container.

### Check the Group's Status
* First, see the current state of your consumer group. Run this command from your host machine's terminal:
```sh
kafka-consumer-groups --bootstrap-server kafka:9092 --describe --group bike-trip-reader
```

* This will show you which partition the consumer is reading, the last committed offset (CURRENT-OFFSET), the very last message offset in the partition (LOG-END-OFFSET), and how many messages the consumer is "behind" (LAG).

### Reset the Group's Offset
* To force the bike-trip-reader group to start over from the very beginning of the topic, run the following command:
```sh
kafka-consumer-groups --bootstrap-server kafka:9092 --reset-offsets --group bike-trip-reader --topic bike_trips_raw_csv --to-earliest --execute
```

* After running the --reset-offsets command, if you start your consumer application again, it will ignore its previously saved position and start reading all messages from offset 0.

Good luck! In the next lab, we will will use the Schema Registry.