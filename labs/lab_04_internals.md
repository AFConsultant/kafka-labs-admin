# Lab 04 - Kafka Internals & Request Flow

In this lab, you will explore **Kafka internals**: the different broker roles, the lifecycle of Kafka requests, and how consumer fetch latency behaves under different load conditions.

---

## Step 1 - Metadata Brokers vs Service Brokers

See your [docker-compose.yaml](../docker-compose.yaml) file. It contains 6 brokers : three metadata brokers (901, 902, 903) and three service brokers (101, 102, 103).

- **Metadata brokers**: These brokers are also **KRaft controllers**. They store the cluster metadata in the KRaft metadata log (replacing ZooKeeper in modern Kafka).
- **Service brokers**: These brokers handle the actual **Produce** and **Fetch** requests from clients.

> 📝 See the `KAFKA_PROCESS_ROLES` configuration of each broker to understand it's assigned role.

To see the controller metadata quorum, use the associated CLI tool:

```bash
kafka-metadata-quorum.sh \
  --bootstrap-server kafka-101:9196 \
  describe --status
```

This shows:

- `CluserId`: the ID of the cluster
- `CurrentVoters`: controllers participating in metadata quorum.
- `CurrentObservers`: brokers that observe metadata but don’t vote.

---

## Step 2 - Kafka Request Types & Processing Stages

Kafka processes different request types:

- **ProduceRequest** → Sent by producers to append records to a topic-partition.
- **FetchRequest (Consumer)** → Sent by consumers to retrieve messages.
- **FetchRequest (Follower)** → Sent by replica followers to fetch from the leader.

**Stages of request processing inside a broker:**

1. **Network layer (network threads)**

- Reads the request from the socket.

2. **Request queue**

- Request waits for an available I/O thread.

3. **I/O thread (worker)**

- Processes the request:
- For **ProduceRequest**: validate, append to log (page cache), maybe enter **purgatory** until replicas ack.
- For **FetchRequest**: read from page cache or disk.

4. **Replica acknowledgement (Producer) or fetch min bytes (Consumer)**

- Requests are parked in the `Purgatory`.
- Wait for `acks=all` or configured value before responding (Producer).
- Wait for the fetch response to reach `fetch.min.bytes`.

5.**Response queue**

- Response is queued for network threads.

6.**Network threads**

- Send the response back to the client.

> 💡 *Purgatory* is an internal waiting room for delayed operations (e.g., produce ack waiting for replication, fetch waiting for new data).

---

## Step 3 - Observing Consumer Fetch Latency in Grafana

**Initial setup:**
Your cluster already has `producer-perf-test` running, producing data to the `perf-test` topic.

Open **Grafana** → Dashboard **Kafka Broker / Performance & Latency**.

Select the `FetchConsumer` requests in the filter.

Find the **Request Total Time Ms** panel. Observe the different latencies.

Note the other existing panels, and how they match the Request lifecycle.

> In a **normal load scenario**, the 95th percentile latency should be **low** (a few milliseconds), because fetch requests return quickly when new messages are available.

---

## Step 4 - Simulate Idle Consumer Fetch Latency

We will now simulate a **lack of new data** to show how `fetch.max.wait.ms` impacts latency.

Stop the producer:

```bash
docker stop producer-perf-test
```

Keep watching Grafana:

- After a few minutes, **Fetch Consumer Request Latency (p95)** should **increase**.
- Expect values close to **500ms** - the default `fetch.max.wait.ms` - because consumers now wait before timing out empty fetches.
- When there are no new messages, consumers wait up to `fetch.max.wait.ms` before returning an empty response.
- This increases the observed fetch latency even though the system is not overloaded.

---

## Step 5 - Restart the Producer

Restart the producer:

```bash
docker start producer-perf-test
```

Observe in Grafana:

- Fetch Consumer Request Latency should drop back to **low values**.
- Consumers Requests are now returned immediately.

---
