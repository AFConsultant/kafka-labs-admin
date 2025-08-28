# Lab 02 - Investigating Durability

In this lab, you'll explore how Kafka ensures data durability through replication, investigate the distributed log structure, and simulate a broker failure and recovery.

---

## Kafka Replication and the Distributed Log

Kafka achieves durability by replicating partitions across multiple brokers.  
If a broker fails, other replicas continue serving data until the broker recovers.

In this lab, you will:

- Create a replicated topic.
- Inspect Kafka’s log files and checkpoint markers.
- Simulate a broker shutdown and observe leader elections.
- Bring the broker back online and verify recovery.

---

## Step 1 - Create the topic

We'll create a topic `replicated-topic` with **6 partitions** and **2 replicas**.

```shell
kafka-topics.sh \
  --bootstrap-server kafka-101:9196 \
  --create \
  --topic replicated-topic \
  --partitions 6 \
  --replication-factor 2
````

Note : replication factor of 2 is not a recommended production settings, but it will be useful for the lab purposes.

**Verify topic creation:**

```shell
kafka-topics.sh \
  --bootstrap-server kafka-101:9196 \
  --describe \
  --topic replicated-topic
```

Notice:

* **Leader**: broker currently serving reads/writes for the partition.
* **Replicas**: all brokers holding a copy of the partition.
* **ISR (In-Sync Replicas)**: replicas fully caught up with the leader.

---

## Step 2 - Produce test data to the topic

We’ll send 6000 messages of 100 bytes at 1000 msgs/sec to `replicated-topic`.

```shell
kafka-producer-perf-test.sh \
  --topic replicated-topic \
  --num-records 6000 \
  --record-size 100 \
  --throughput 1000 \
  --producer-props bootstrap.servers=kafka-101:9196
```

Then, produce a few manual messages:

```shell
kafka-console-producer.sh \
  --bootstrap-server kafka-101:9196 \
  --topic replicated-topic
```

Example:

```
>msg1
>msg2
>msg3
>msg4
>msg5
# CTRL-D to exit
```

---

## Step 3 - Inspect the topic files inside the brokers

On each broker, Kafka stores checkpoint files that indicate:

* **`recovery-point-offset-checkpoint`** - last offset flushed to disk.
* **`replication-offset-checkpoint`** - high water mark (last committed offset).

Example (inside broker container):

```shell
docker exec -it kafka-101 bash
grep replicated-topic /opt/kafka/data/recovery-point-offset-checkpoint
grep replicated-topic /opt/kafka/data/replication-offset-checkpoint
# CTRL-D to exit
```

Compare offsets across brokers - they may differ slightly due to partition distribution.
You will notice that data is not flushed to disk yet. Is that a problem ? How does Kafka ensure durability ?

---

## Step 4 - Explore the segments files

Kafka stores partition data in:

* `.log` - actual messages.
* `.index` - mapping offsets → byte positions.
* `.timeindex` - mapping timestamps → offsets.
* `leader-epoch-checkpoint` - history of partition leadership.

Example:

```shell
docker exec -it kafka-101 bash
ls -l /opt/kafka/data/replicated-topic-0
cat /opt/kafka/data/replicated-topic-0/leader-epoch-checkpoint
```

Note : you might get the error `ls: cannot access '/opt/kafka/data/replicated-topic-0': No such file or directory`. Why is that ?
Remember that not all brokers contain all partitions.
Inspect the topic again to check if the partition 0 is on the 101 broker.
If that's not the case, run the same commands using a different topics.

The `leader-epoch-checkpoint` file tracks the change of leadership of the partitions.
The first line is the version of protocol of the file, the second line is the number of leadership change (= epochs), and the line after that is the epoch and the offset at current epoch.

```text
0   <- protocol
1   <- number of epochs
0 0 <- epoch and offset
```

If there is a new leader elected, the file would change like this:

```text
0 
2      <- 2 epochs or change of leadership
0 0    <- offset at epoch 0
1 1234 <- offset at epoch 1
```

The `leader-epoch-checkpoint` is used internally by Kafka to truncate incorrect logs in case of a leadership change.

You can also inspect the log contents:

```shell
/opt/kafka/bin/kafka-dump-log.sh \
  --print-data-log \
  --files /opt/kafka/data/replicated-topic-0/00000000000000000000.log
```

Try to find any of your messages that you wrote earlier in one of the partition.

Exit your docker shell sessions if needed by pressing `CTRL+D`.

---

## Step 5 - Simulate a broker failure

Pick the broker that is a leader for the partition 0 of replicated-topic (describe the topic if you need). Note it's ID (`kafka-101`, `kafka-102` or `kafka-103`). We will shutdown this broker (replace the id with what you found):

```shell
export OFFLINE_BROKER=kafka-101
docker-compose stop $OFFLINE_BROKER
```

Check running brokers:

```shell
docker-compose ps
```

Re-describe the topic to see new leaders:

```shell
kafka-topics.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --describe \
  --topic replicated-topic
```

Note that We use the full boostrap server here, instead of just kafka-101, because it might be shutdown and we still want to access the cluster.
Also note that partitions previously led by `$OFFLINE_BROKER` now have new leaders (such as partition 0).

Let's inspect our cluster health by checking Grafana.

Use the port tab to connect to Grafana (port 3000). If the port doesn't exist, add it manually.
Log in using `admin` / `kafka`.
Go to the `Kafka Cluster / Global HealthCheck` dashboard. 
How many brokers are running ? How many partitions are under-replicated ? Why ?
In a production scenario, should we act if a broker is offline ?

---

## Step 6 - Bring the broker back online

Restart the broker:

```shell
docker-compose start $OFFLINE_BROKER
```

After a few minutes:

* **Under-replicated partitions** should return to **0**.
* Leaders may rebalance back to preferred replicas.

---