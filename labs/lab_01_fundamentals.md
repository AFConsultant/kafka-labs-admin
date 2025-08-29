# Lab 01 - Kafka first steps

## What's inside the Kafka Stack

The [docker-compose file](../docker-compose.yaml) contains an entire Kafka environment.

A Kafka environment consists of:

- One or more brokers grouped into a cluster. In this case, there is multiple brokers, metadata brokers (kafka-901, 902 and 903) and service brokers (kafka-101, 102 and 103).
- [AKHQ](https://akhq.io/). This app allows you to see what's inside your cluster (port: 8085).
- The Schema Registry, which contains your environment schemas.
- Services that automatically creates topics, users, ACL, and trigger the metrics collection.
- Prometheus and Grafana to handle the metrics.
- Two test clients, producer-perf-test, and consumer-perf-test, to generate load.
- A opentelemetry collector to collect metrics from the broker.
- A kafka exporter that exposes lag.

## Setting up your environment

Run the following ansible script to setup your environment :

```sh
ansible-playbook ansible/main.yml
```

Once it's done, open a new terminal.

## Creating your first topic

First, let's create a topic.

To do this, we will use the `kafka-topic` command line tool.

This tool allows you to create, update, delete, or describe a topic. Let's create the topic : `first_topic`.

```shell
kafka-topics.sh \
  --bootstrap-server kafka-101:9196 \
  --create \
  --topic first_topic \
  --partitions 3 \
  --replication-factor 3
```

If the creation is successful, a message should appear: `Created topic first_topic.`. 
Congratulations.

Note that the argument `bootstrap-server` was necessary. This can be any broker of the Kafka Cluster. This broker will be queried to discover other brokers in the cluster.
In production, it is recommended to use at east 3 brokers - that way, if one of the brokers is down, the others can still be used for the discovery. Example: `kafka-101:9196,kafka-2:9296,kakfa-3:9396`.

You can describe the topic you just created:

```shell
kafka-topics.sh \
  --bootstrap-server kafka-101:9196 \
  --describe \
  --topic first_topic
```

Check the API of `kafka-topics` to see how you can delete the topic you created.

You can also use AKHQ to manage your topics. In production, using a tool like Terraform is recommended to let your CI/CD manage topics.

Topics can be auto-created by default by producer and consumers. This is a bad practice. This behaviour has been disabled in this environment, check the parameter `KAFKA_AUTO_CREATE_TOPICS_ENABLE` in the [docker-compose file](../docker-compose.yaml).

### It's your turn now

Create a topic 'hello-world', with 5 partitions and a replication-factor of 3.

## Let's produce and consume

Other Kafka CLI are interesting.

`kafka-console-producer` is the tool used to push messages into Kafka.

Make sure you created the `hello-world` topic before continuing.

First, run the `kafka-console-producer` without any arguments to show the documentation.

Try to come up with the command by yourself.

```shell
kafka-console-producer.sh \
  --bootstrap-server kafka-101:9196 \
  --topic hello-world
```

Now, type your messages, press enter: your message is sent to Kafka. Press `CTRL-D` to exit.

```shell
>my first message
>my other message
> #CTRL-D PRESSED
```

Your messages can be seen in AKHQ (see the Ports tab, and check for the `8085` port). Click on the 🔎 next to the topic.

Your messages can also be consumed using another CLI: `kafka-console-consumer`.

Try to find the command by yourself, reading the documentation. One additionnal argument will be necessary.

```shell
kafka-console-consumer.sh \
  --bootstrap-server kafka-101:9196 \
  --topic hello-world \
  --from-beginning
```

Press `CTRL-C` to stop consuming messages. You should see the messages you sent earlier.

```shell
my first message
my other message
^CProcessed a total of 2 messages
```