# Lab 05 - Cluster Configuration and Management

This lab explores how to manage and configure a Kafka cluster. You will learn how to inspect broker configurations, rebalance data across brokers for optimal performance, and simulate and recover from a complete broker failure.

## Exploring Broker Configuration

In production, you will often need to fine-tune your cluster's behavior. Kafka provides hundreds of configuration properties. Let's get familiar with where to find them.

1. Connect to **AKHQ**.
2. Go to your cluster nodes.
3. Click on the 🔎 on any node.
4. Take a moment to explore the different configurations.

You'll notice that some settings are `DEFAULT_CONFIG` and other `STATIC_BROKER_CONFIG`.
`STATIC_BROKER_CONFIG` comes from the `server.properties` file. This file is generated from our docker-compose environment variables.

Dynamically Changing Configuration
One of Kafka's most powerful features is the ability to change most configurations on-the-fly without restarting brokers. Let's test this by changing the data retention period for a new topic.

1. Create a Test Topic and Check its Configuration
First, create a simple topic called retention-test.

```shell
kafka-topics.sh \
  --bootstrap-server kafka-101:9196 \
  --create \
  --topic retention-test \
  --partitions 3 \
  --replication-factor 3
```

This topic does not have any special configuration.

```shell
kafka-configs.sh \
  --bootstrap-server kafka-101:9196 \
  --describe \
  --topic retention-test
```

2. Alter the Configuration and Verify the Effect

Let's dynamically change the retention time to just 10 seconds (10,000 milliseconds) instead of 7 days. This is an unreasonably short time for production but perfect for a quick test. We also change the `segment.ms` to make sure the segment will close rapidly.

```shell
kafka-configs.sh \
  --bootstrap-server kafka-101:9196 \
  --alter \
  --topic retention-test \
  --add-config log.retention.ms=10000,segment.ms=5000
```

Check that the configuration has been applied properly.

```shell
kafka-configs.sh \
  --bootstrap-server kafka-101:9196 \
  --describe \
  --topic retention-test
```

Now, let's see this change in action:

Produce a message:

```shell
kafka-console-producer.sh \
  --bootstrap-server kafka-101:9196 \
  --topic retention-test

>Hello, I will be deleted soon!
# Press CTRL+D to exit
```

Wait for 15 seconds for the retention period to pass and for Kafka's log cleaner to run.

```shell
echo "Waiting for 15 seconds..."
sleep 15
echo "Done waiting."
```

Try to consume the message:

```shell
kafka-console-consumer.sh \
  --bootstrap-server kafka-101:9196 \
  --topic retention-test \
  --from-beginning \
  --timeout-ms 5000
```

You'll notice the consumer exits after 5 seconds with a `TimeoutException`. The message we sent has been automatically deleted because of the new retention policy we applied! ✅

3. Revert the Configuration

Let's remove our dynamic override, which will cause the topic to revert to the cluster's default retention policy.

```shell
kafka-configs.sh \
  --bootstrap-server kafka-101:9196 \
  --alter \
  --topic retention-test \
  --delete-config retention.ms,segment.ms
```

-----

## Rebalancing the Cluster

A common administrative task is rebalancing data. This is often necessary when adding new brokers to a cluster or when data distribution becomes skewed, leading to "hotspots" on certain brokers.

### 1\. Create an Imbalanced Topic

First, let's intentionally create a topic with all its data on only two of our three brokers to simulate an imbalance.

Create a new topic named `moving-parts` with 6 partitions and 2 replicas, but place them only on brokers 1 and 2. 

```shell
# Command from the PDF adapted for the sample lab's environment
kafka-topics.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9196 \
  --create \
  --topic moving-parts \
  --replica-assignment 101:102,102:101,101:102,102:101,101:102,102:101
```

Next, produce a significant amount of data (about 2GB) to this topic. 

```shell
# Command from the PDF adapted for the sample lab's environment
kafka-producer-perf-test.sh \
  --topic moving-parts \
  --num-records 2000000 \
  --record-size 1000 \
  --throughput -1 \
  --producer-props bootstrap.servers=kafka-101:9196,kafka-102:9196
```

### 2\. Observe the Imbalance

Go to the Control Center and navigate to the **Brokers** overview page. You should see a metric for **Disk** usage. After a few minutes, a red bar may appear, indicating a significant imbalance in data distribution across the brokers.  You can also inspect the `moving-parts` topic to confirm all its partitions reside on brokers 1 and 2. 

### 3\. Execute and Monitor the Rebalance

To fix this, we will use the `confluent-rebalancer` tool. This tool calculates a plan to move partition replicas around the cluster to achieve a more even load.

Execute the rebalance. We will add a `--throttle` flag to limit the rebalancing network traffic to 1MB/s so it doesn't impact other cluster operations. 

```shell
# This command is from the Confluent Platform, your environment might need a different tool
confluent-rebalancer execute \
  --bootstrap-server localhost:9092 \
  --throttle 1000000
```

The tool will present a plan and ask for confirmation. Type `y` to proceed. 

Once started, the rebalancer sets dynamic configuration on the brokers to limit replication traffic. You can monitor the progress:

```shell
# Check the status of the ongoing rebalance
confluent-rebalancer status --bootstrap-server localhost:9092

# [cite_start]You'll see which partitions are currently being moved 
Partitions being rebalanced:
 Topic moving-parts: 0,1,2,4
```

The rebalance will eventually complete.  Check the **Brokers** page in Control Center again; the disk usage should now be balanced.  If you inspect the `moving-parts` topic, you will see its partitions are now distributed across all three brokers. 

-----

## Simulate and Recover from a Broker Failure

Kafka is designed for high availability. Let's simulate a permanent broker failure to see how the cluster recovers.

### 1\. Fail a Broker

We will completely stop broker 1 and wipe its data volume, simulating a catastrophic hardware failure. 

```shell
# The following commands use the docker-compose setup from the PDF
docker-compose stop kafka-1
docker-compose rm kafka-1
docker volume rm confluent-admin_data-kafka-1
```

In Control Center, you will see the broker count drop to 2, and more importantly, you'll see "Under replicated partitions."  This is because the replicas that lived on broker 1 are now gone. The cluster is in an unhealthy state.

### 2\. Replace and Recover the Broker

Now, let's "replace" the failed broker by starting a new, empty one with the same ID. 

```shell
docker-compose up -d kafka-1
```

The new broker will start up with no data.  Because the topic `moving-parts` had a replication factor of 2, the surviving brokers (2 and 3) still have a complete copy of the data. The cluster controller will instruct the new broker 1 to copy the missing data from the other brokers.

After a few minutes, you can check the logs on the new broker 1. You will see that the topic data has been replicated to it, automatically healing the cluster.  The number of "Under replicated partitions" will return to zero, and the cluster will be fully operational and healthy again. 

### Cleanup

To clean up your environment and remove all containers and volumes, run: 

```shell
docker-compose down -v
```
