# Lab 03 — Producers, Consumers & Monitoring Lag

## Initial situation

1. Reset the environment

```bash
docker-compose down -v && docker-compose up -d
````

Wait until all services are healthy.

2. Open Grafana and go to the global healthcheck dashboard

Use the port tab to connect to Grafana (port 3000). If the port doesn't exist, add it manually.
Log in using `admin` / `kafka`.
Go to the `Kafka Cluster / Global HealthCheck` dashboard. 
How many brokers are running ? How many partitions are under-replicated ? Why ?
In a production scenario, should we act if a broker is offline ?

Observe:

* Messages in per topic
* Messages in per second
* Bytes in per second
* Bytes out per second

Why are *bytes in* and *bytes out* greater than 100 bytes when our record size is 100 bytes?

3. Open AKHQ

Go to Topics → find `perf-test`.
You should see the consumer group `consumer-perf-test` associated with this topic and a lag value.
Go to Consumer Groups → `consumer-perf-test`
Inspect lag per partition (Current offset vs End offset).

> ⚠️ Tracking lag via a static view is not ideal. We want trend: is lag constant, growing, or shrinking?

4. Back to Grafana → “Kafka Consumers / Fetch Rate & Records Lag (perf-test topic)”

What are the min and max lag variations over the observed window?
Is the lag “normal” for your cluster load? (Stable or slowly shrinking is typically fine; steadily growing suggests under-consumption.)

5. Grafana → “Producer Performance (client.id=producer-perf)”

What’s the difference between producer request rate and send rate?
What is latency here?

---

## Stop the consumer and manipulate the lag

1. Stop the consumer container

```bash
docker stop consumer-perf-test
```

2. Check Grafana

Does lag start growing? (It should, since the producer keeps writing and no one is consuming.)
In AKHQ, confirm the growing lag for `consumer-perf-test` on `perf-test`.

3. Describe the consumer group (CLI)

```bash
kafka-consumer-groups.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --describe \
  --group consumer-perf-test
```

Note current offsets, end offsets, and lag per partition.

4. Reset offsets to earliest (simulate reprocessing; increases lag immediately)

```bash
kafka-consumer-groups.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --reset-offsets \
  --to-earliest \
  --group consumer-perf-test \
  --topic perf-test \
  --execute
```

How did the lag curve behave in Grafana right after the reset?

5. Reset offsets to latest (skip backlog; set lag \~0)

```bash
kafka-consumer-groups.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --reset-offsets \
  --to-latest \
  --group consumer-perf-test \
  --topic perf-test \
  --execute
```

How did the lag curve behave in Grafana now?

> 💡 Tip: Use `--dry-run` before `--execute` to preview changes safely.

---

## Restart the consumer and add a new one

1. Restart the consumer

```bash
docker start consumer-perf-test
```

In Grafana, watch lag trend. Is it returning to normal (near zero or stable)?
In AKHQ, confirm the group member is present and consuming.

2. Add another consumer to the same group (share the load)

Open a new terminal:

```bash
kafka-console-consumer.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --topic perf-test \
  --group consumer-perf-test
```

In Grafana, you should see multiple consumer instances for the same group.
In AKHQ, two members in `consumer-perf-test`.

3. Add a third consumer (same group) in a new terminal

```bash
kafka-console-consumer.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --topic perf-test \
  --group consumer-perf-test
```

Does lag decrease as more consumers share the partitions?
How many more consumers can we add to this group to improve throughput?

### Optional Challenge

* Change the producer rate (e.g., run your own `kafka-producer-perf-test.sh` with higher `--throughput`) and observe lag impact.
* Add partitions to `perf-test` and see if adding more consumers helps further.
* Create a second consumer group for `perf-test` and compare its lag vs the original group.
* Think about alerts: which Grafana/Prometheus signals would you alert on for `consumer falling behind` vs `producer overloaded`?

> 🧯 Safety Note: Never reset offsets in production without a rollback plan and stakeholder approval.
