# Lab 05 - Cluster Configuration and Management

This lab explores how to manage and configure a Kafka cluster. You will learn how to inspect broker configurations, rebalance data across brokers for optimal performance, and simulate and recover from a complete broker failure.

## Exploring the broker configuration

In production, you will often need to fine-tune your cluster's behavior. Kafka provides hundreds of configuration properties. Let's get familiar with where to find them.

1. Connect to **AKHQ**.
2. Go to your cluster nodes.
3. Click on the 🔎 on any node.
4. Take a moment to explore the different configurations.

You'll notice that some settings are `DEFAULT_CONFIG` and other `STATIC_BROKER_CONFIG`.
`STATIC_BROKER_CONFIG` comes from the `server.properties` file. This file is generated from our docker-compose environment variables.

**Dynamically Changing Configuration**

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
  --add-config "retention.ms=10000,segment.ms=5000"
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

Wait for a few minutes for the cleaner thread to run. It can take up to five minutes.

Ten try to consume the message:

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

## Rebalancing the cluster

A common administrative task is rebalancing data. This is often necessary when adding new brokers to a cluster or when data distribution becomes skewed, leading to "hotspots" on certain brokers.

### Create an imbalanced topic

First, let's intentionally create a topic with all its data on only two of our three brokers to simulate an imbalance.

Create a new topic named `moving-parts` with 6 partitions and 2 replicas, but place them only on brokers 1 and 2.

```shell
kafka-topics.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --create \
  --topic moving-parts \
  --replica-assignment 101:102,102:101,101:102,102:101,101:102,102:101
```

Note that the assignment only created replicas on brokers 101 and 102, and not 103.
Next, we will produce a significant amount of data (about 2GB) to this topic.

```shell
kafka-producer-perf-test.sh \
  --topic moving-parts \
  --num-records 2000000 \
  --record-size 1000 \
  --throughput -1 \
  --producer-props bootstrap.servers=kafka-101:9196,kafka-102:9296,kafka-103:9396
```

### Observe the imbalance

Open **Grafana** → Dashboard **Kafka / Hard Disk Usage**.
Find the **Logs Size Per Broker (including replicas)** panel. Observe the difference in disk usage for each brokers.
We will rebalance the replicas in your cluster for the topic `moving-parts`.

### Rebalance the partitions

We will use the `kafka-reassign-partitions` CLI to rebalance the partitions.
First, we will create a reassignment plan. Create a json file named `reassignment-plan.json`.

```json
{
  "version": 1,
  "topics": [
    { "topic": "moving-parts" }
  ]
}
```

Let's create a partition reassignment plan from this plan.
Execute the following command:

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --topics-to-move-json-file reassignment-plan.json \
  --broker-list 101,102,103 \
  --generate
```

The proposed partition reassignment plan rebalances partitions accross the cluster.
Save this plan to a json file `reassignment.json`.

Execute the rebalance based on this plan. We will use a throttle of 5 MB / s.  

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --reassignment-json-file reassignment.json \
  --execute \
  --throttle 5000000
```

You should see a message indicated the partition reassignment started:

```text
Successfully started partition reassignments for moving-parts-0,moving-parts-1,moving-parts-2,moving-parts-3,moving-parts-4,moving-parts-5
```

The reassignment should take around 7 minutes (approximately 2 GB of data moved at 5 MB / s) at most.
Check the new partition assignment of the topic `moving-parts` in AKHQ.
See the disk usage increase in Grafana.

Regularly check the status of the `kafka-reassign-partitions` CLI tool:

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --reassignment-json-file reassignment.json \
  --verify
```

When it's completed, you should see :

```text
Status of partition reassignment:
Reassignment of partition moving-parts-0 is completed.
Reassignment of partition moving-parts-1 is completed.
Reassignment of partition moving-parts-2 is completed.
Reassignment of partition moving-parts-3 is completed.
Reassignment of partition moving-parts-4 is completed.
Reassignment of partition moving-parts-5 is completed.
```

The disk usage in Grafana should now show a better balance. Compare the situation before and after the rebalance.

-----

## Simulate and Recover from a Broker Failure

Kafka is designed for high availability. Let's simulate a permanent broker failure to see how the cluster recovers.

### Fail a Broker

We will completely stop broker 101 and wipe its data volume, simulating a catastrophic hardware failure.

```shell
docker-compose stop volume-init
docker-compose rm volume-init
docker-compose stop kafka-101
docker-compose rm kafka-101
docker volume rm kafka-labs-admin_data-broker-101
```

Open **Grafana** → Dashboard **Kafka Cluster / Global HealthCheck**.

You should see after a while two broker running and some under-replicated partitions.
We will now recover the broker by simply restarting it.

### Replace and Recover the Broker

Now, let's "replace" the failed broker by starting a new, empty one with the same ID.

```shell
docker-compose up -d kafka-101
```

The new broker will start up with no data. It will first become a follower on each replica it was assigned.

After a few minutes, you can check the logs on the new broker 101, for the `perf-test` topic.

```shell
docker exec kafka-101 ls /opt/kafka/data | grep perf-test
```

You will see that the topic data has been replicated to it, automatically healing the cluster.  The number of "Under replicated partitions" will return to zero, and the cluster will be fully operational and healthy again.
