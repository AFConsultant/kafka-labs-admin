# Lab 05 - Kafka Monitoring

## Metric monitoring

They are two kind of metrics to monitor in Kafka:

- The cluster metrics
- The client metrics

Both are useful to solve different kind of problems.

### Cluster metrics

Each broker in the cluster has metrics accessible using JMX.
In this lab, we will not use JMX directly to expose the cluster metrics but instead use an exporter. This exporter will export the metrics to Prometheus.

#### Exporter setup

Look at the `docker-compose.yaml` file. For each service kafka brokers (101, 102, 103), an environment variable `KAFKA_OPTS` is used. This variable loads a java agent that will expose the metrics. We use the JMX prometheus exporter with a dedicated configuration file in the `~/etc/jmx/` folder.

YAML files in this folder show which metrics we export to prometheus. As you can see, they are a lot of metrics and it can be cumbersome to configure properly.

The metrics are exported to prometheus every 10 seconds.

Go to Prometheus (port `9090`) and query some interresting metrics to check they are properly collected.

For example, the total number of partitions for the topic `perf-test` (6 partitions * 3 replicas):

```text
sum(kafka_cluster_partition_ReplicasCount{topic="perf-test"})
```

The number of active bokers:

```text
kafka_controller_ActiveBrokerCount
```

The active controller:

```text
kafka_controller_ActiveControllerCount
```

The number of bytes in / s for the `perf-test` topic:

```text
kafka_server_broker_topic_metrics_BytesInPerSec_rate{topic="perf-test"}
```

#### Grafana dashboards

These metrics are then used in the Grafana dashboards you saw previously.

**Your task:**
>Create a new dashboard in Grafana and create graphs for **3** metrics of your choice.
The list of metrics can be found in the Kafka documentation.

### Client metrics

Besides the cluster metrics, it is also important to collect client metrics to understand how they work. This is a little bit more involved.

Clients using the `kafka-clients` java library can expose they metrics directly to prometheus.

Since Kafka version 3.7.0 (KIP-714), the client metrics can be collected by the broker automatically.

To allow this, you must:

- Code a custom metrics Reporter, which is a java class implementing MetricsReporter, ClientTelemetry. This has been done for you, see the `code/java/metrics-reporter` project.
- Package and export this class as a JAR, and make it accesible to the brokers. See the `/etc/broker/libs/metrics-reporter.jar`. This jar file is added as a volume for each broker.
- Configure the brokers to use this repoter using the `KAFKA_METRIC_REPORTERS` env variable. You can also configure the collector endpoint (where the metrics will be exported) using the `OTEL_EXPORTER_COL_ENDPOINT` env variable.
- The OpenTelemetry exporter will receive these metrics by GRPC and expose them to HTTP.
- Prometheus will scrape this metrics using HTTP.
- You also need to create a metric subscription on the brokers. For this, you need to run the `kafka-client-metrics.sh` CLI tool. This is done in our environment using the `start-metrics-collection` service in docker.

As you can see, this is fairly complicated, and fortunately everything has been done for you in this lab.

You can see the result in Prometheus. Try to query a few interresting client metrics, such as the producer byte rate:

```txt
org_apache_kafka_consumer_outgoing_byte_rate
```

Or the record send rate:

```txt
org_apache_kafka_producer_record_send_rate
```

Or the consumer fetch rate:

```txt
org_apache_kafka_consumer_fetch_manager_fetch_rate
```

What metrics would you monitor for your clients ?
Note that we will talk about the lag more in the next sections.
Complete the Grafana dashboad you previously created with 3 of these metrics.

## Lag Monitoring

Unfortunately, the lag is not exported in the JMX metrics in standard Kafka. Some metrics exist but they are hard to use and not very powerful.

To monitor the lag, we will use another exporter, the kafka-exporter. This exporter will export the lag as HTTP metrics that prometheus will collect.

See the `kafka-exporter` service in the docker-compose file.
You can see the metrics directly in HTTP. Access the `9308` port. This will show all additional metrics exported. Search for the `lag` metrics.

Query these metrics in prometheus.
Add some lag metrics in Grafana in the dashboard you previously created.
