# Lab 01 - Kafka first steps

## What's inside the Kafka Stack

The [docker-compose file](../docker-compose.yaml) contains an entire Kafka environment.

A Kafka environment consists of:

- One or more brokers regrouped into a cluster. In this case, there is only a single broker, which exposes ports 9092.
- [AKHQ](https://akhq.io/). This app allows you to see what's inside your cluster (port: 8085).
- The Schema Registry, which contains your environment schemas.
- A pod that auto creates topics.
- A ksqlDB server
- A Kafka Connect Worker
- A Postgres' database, used for this lab only but not necessary for Kafka per say.

## Introducing Citi Bike

In this lab, we will use the `Citi Bike` dataset.
The `Citi Bike` dataset refers to the public, openly available data released by the Citi Bike program (New York City’s bike-sharing network), including extensive trip histories, station info, and real-time availability feeds.

We will use publicly data released in 2013 regrading bike trips.

Check out the [dataset](/data/201306-citibike-tripdata_1_6K.csv).

This represents bike trips, from one station to another.

| tripduration | starttime           | stoptime            | start station id | start station name     | start station latitude | start station longitude | end station id | end station name        | end station latitude | end station longitude | bikeid | usertype   | birth year | gender |
|--------------|---------------------|----------------------|-------------------|-------------------------|--------------------------|---------------------------|----------------|--------------------------|-----------------------|------------------------|--------|------------|------------|--------|
| 695          | 2013-06-01 00:00:01 | 2013-06-01 00:11:36  | 444               | Broadway & W 24 St      | 40.7423543              | -73.98915076              | 434            | 9 Ave & W 18 St         | 40.74317449           | -74.00366443           | 19678  | Subscriber | 1983       | 1      |
| 693          | 2013-06-01 00:00:08 | 2013-06-01 00:11:41  | 444               | Broadway & W 24 St      | 40.7423543              | -73.98915076              | 434            | 9 Ave & W 18 St         | 40.74317449           | -74.00366443           | 16649  | Subscriber | 1984       | 1      |
| 2059         | 2013-06-01 00:00:44 | 2013-06-01 00:35:03  | 406               | Hicks St & Montague St | 40.69512845             | -73.99595065              | 406            | Hicks St & Montague St | 40.69512845           | -73.99595065           | 19599  | Customer   | 0          | 0      |

## Goal

Each trip is a bike that departs from a station, and arrives to another station after a certain duration.

We will send this data to Kafka in this lab using CLI tools.

## Setup the necessary topics.

First, let's create a topic that will contain our CSV data.

To do this, we will use the `kafka-topic` command line tool.

This tool allows you to create, update, delete, or describe a topic. Let's create the topic : `bike_trips_raw_csv`.

```shell
kafka-topics \
  --bootstrap-server kafka:9092 \
  --create \
  --topic bike_trips_raw_csv \
  --partitions 3 \
  --replication-factor 1
```

If the creation is successful, a message should appear: `Created topic bike_trips_raw_csv.`. Congratulations.

Note that the argument `bootstrap-server` was necessary. This can be any broker of the Kafka Cluster. This broker will be
queried to discover other brokers in the cluster.
In production, it is recommended to use at east 3 brokers - that way, if one of the brokers is down, the others can still
be used for the discovery. Example: `kafka-1:9092,kafka-2:9092,kakfa-3:9092`.

You can describe the topic you just created:

```shell
kafka-topics \
  --bootstrap-server kafka:9092 \
  --describe \
  --topic bike_trips_raw_csv
```

Check the API of `kafka-topics` to see how you can delete the topic you created.

You can also use AKHQ to manage your topics. In production, using a tool like Terraform is recommended to let your CI/CD
manage topics.

Topics can be auto-created by default by producer and consumers. This is a bad practice. This behaviour has been disabled
in this environment, check the parameter `KAFKA_AUTO_CREATE_TOPICS_ENABLE` in
the [docker-compose file](../docker-compose.yaml).

### It's your turn now

Create a topic 'hello-world', with 5 partitions and a replication-factor of 1.

## Let's produce and consume

Other Kafka CLI are interesting.

`kafka-console-producer` is the tool used to push messages into Kafka.

We will push the first five bike trips to the topic we previously created, `bike_trips_raw_csv`.

First, run the `kafka-console-producer` without any arguments to show the documentation.

Try to come up with the command by yourself.

```shell
kafka-console-producer \
  --bootstrap-server kafka:9092 \
  --topic bike_trips_raw_csv
```

Now, type your messages, press enter: your message is sent to Kafka. Press `CTRL-D` to exit.

```shell
>695,2013-06-01 00:00:01,2013-06-01 00:11:36,444,Broadway & W 24 St,40.7423543,-73.98915076,434,9 Ave & W 18 St,40.74317449,-74.00366443,19678,Subscriber,1983,1
>693,2013-06-01 00:00:08,2013-06-01 00:11:41,444,Broadway & W 24 St,40.7423543,-73.98915076,434,9 Ave & W 18 St,40.74317449,-74.00366443,16649,Subscriber,1984,1
> #CTRL-D PRESSED
```

Your messages can be seen in [AKHQ](http://localhost:8085). Click on the 🔎 next to the topic.

Your messages can also be consumed using another CLI: `kafka-console-consumer`.

Try to find the command by yourself, reading the documentation. One additionnal argument will be necessary.

```shell
kafka-console-consumer \
  --bootstrap-server kafka:9092 \
  --topic bike_trips_raw_csv \
  --from-beginning
```

Press `CTRL-C` to stop consuming messages. You should see the messages you sent earlier.

```shell
695,2013-06-01 00:00:01,2013-06-01 00:11:36,444,Broadway & W 24 St,40.7423543,-73.98915076,434,9 Ave & W 18 St,40.74317449,-74.00366443,19678,Subscriber,1983,1
693,2013-06-01 00:00:08,2013-06-01 00:11:41,444,Broadway & W 24 St,40.7423543,-73.98915076,434,9 Ave & W 18 St,40.74317449,-74.00366443,16649,Subscriber,1984,1
^CProcessed a total of 2 messages
```