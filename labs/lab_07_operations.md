# Lab 07 - Operations

## Lab objectives

In this lab we will create a new problem in our Kafka cluster. 
It will be your goal to debug it, and found the appropriate solution.

## Setup

Make sure that your Kafka cluster is running:

```shell
docker-compose up -d
```

Introduce a problem by running a script. You don't know what went wrong in the cluster.

```shell
./create_problem_in_cluster.sh
```

## Use Grafan and the docker logs to find the problem

A producer of *client.id* `producer-perf` is reporting some latency issue.

Use the Grafana dashboards you have at your disposal to find out what's the problem.

You can also consult the logs in the brokers and the producer:

```shell
docker logs kafka-101
docker logs kafka-102
docker logs kafka-103
docker logs producer-perf-test
```

## Identify the source of the producer high latency

Can you confirm that the producer has a high latency ?

Why does the producer have a high latency ?

Is the problem in the producer side or broker side ?

Is the problem on every broker or on a specific broker ?

Try to use the information you have at your disposal to establish a clear view of the problem.

## Report your findings to your instructor

Once you have a lead, go find your instructor and share your conclusions with the class.

Think about the problem and potential solutions. Is the problem directly related to Kafka ?

Would a change in configuration solve the problem ? Which change ?

If the problem is not related to Kafka, what *could* we do about it ?

## The problem and it's solution

**SPOILER** : Do not read this until your instructor tells you you can move forward.

You might have guessed correctly that the problem is originating from a network issue related to `kafka-103`.

The network for this broker is unstable and the impact is a delay measured in seconds for every request.

The easy solution would be to let the network team solve this poblem. But we will try to address it on the Kafka side.

Let's try to replace our faulty broker with a new boker, without impacting the producer and the consumer.

First, we'll add a new service, `kafka-104` to our docker-compose file: 

```yaml
  kafka-104:
    image: apache/kafka:${KAFKA_102_VERSION}
    container_name: kafka-104
    ports:
      - "9496:9496"
    environment:
      KAFKA_NODE_ID: 104
      KAFKA_PROCESS_ROLES: broker
      KAFKA_LISTENERS: PLAINTEXT://kafka-104:9092,REPLICATION://kafka-104:9093,INTERNAL://kafka-104:9094,EXTERNAL://kafka-104:9095,HOST://kafka-104:9496
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka-104:9092,REPLICATION://kafka-104:9093,INTERNAL://kafka-104:9094,EXTERNAL://kafka-104:9095,HOST://kafka-104:9496
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:SASL_SSL,REPLICATION:SASL_SSL,INTERNAL:SASL_PLAINTEXT,EXTERNAL:SASL_SSL,PLAINTEXT:PLAINTEXT,HOST:PLAINTEXT
      KAFKA_SASL_ENABLED_MECHANISMS: PLAIN,SCRAM-SHA-512
      KAFKA_SASL_MECHANISM_INTER_BROKER_PROTOCOL: PLAIN
      KAFKA_SASL_MECHANISM_CONTROLLER_PROTOCOL: PLAIN
      KAFKA_OPTS: "-javaagent:/opt/kafka/config/monitoring/jmx_prometheus_javaagent-1.3.0.jar=/opt/kafka/config/monitoring/config_kafka104.yml -Djava.security.auth.login.config=/opt/kafka/config/jaas/broker.conf"
      KAFKA_SSL_KEYSTORE_LOCATION: /opt/kafka/security/kafka-104.keystore.jks
      KAFKA_SSL_TRUSTSTORE_LOCATION: /opt/kafka/security/kafka-104.truststore.jks
      KAFKA_SSL_KEYSTORE_PASSWORD: kafka-labs
      KAFKA_SSL_KEY_PASSWORD: kafka-labs
      KAFKA_SSL_TRUSTSTORE_PASSWORD: kafka-labs
      KAFKA_CONTROLLER_QUORUM_VOTERS: 901@kafka-901:9093,902@kafka-902:9093,903@kafka-903:9093
      KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
      KAFKA_INTER_BROKER_LISTENER_NAME: REPLICATION
      KAFKA_AUTHORIZER_CLASS_NAME: org.apache.kafka.metadata.authorizer.StandardAuthorizer
      KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND: "${KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND}"
      KAFKA_SUPER_USERS: User:controller_user;User:broker_user;User:admin
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 3
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 2
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "false"
      KAFKA_LOG_DIRS: /opt/kafka/data
      OTEL_EXPORTER_COL_ENDPOINT: "otel-collector:4317"
      KAFKA_METRIC_REPORTERS: com.afconsultant.kafkaadminlabs.ClientOtlpMetricsReporter
    volumes:
      - data-broker-104:/opt/kafka/data
      - ./security/data/kafka-104-creds:/opt/kafka/security:ro
      - ./security/clients:/opt/kafka/clients:ro
      - ./security/jaas/broker.conf:/opt/kafka/config/jaas/broker.conf:ro
      - ./etc/jmx:/opt/kafka/config/monitoring:ro
      - ./etc/broker/libs/metrics-reporter.jar:/opt/kafka/libs/metrics-reporter.jar:ro
    healthcheck:
      test: nc -z kafka-104 9092
      interval: 2s
      timeout: 10s
      retries: 10
      start_period: 5s
    depends_on:
      kafka-901:
        condition: service_healthy
      kafka-902:
        condition: service_healthy
      kafka-903:
        condition: service_healthy
      volume-init:
        condition: service_started
```

Then, we will use the `kafka-reassign-partitions` CLI tool to reassign partitions from the broker `kafka-103` to the broker `kafka-104`.

Think about this for a second. Can we directly reassign partitions from `kafka-103` to `kafka-104` ? What challenge could we face ?

The problem is that if we move the leadership of the `perf-test` partitions directly from `kafka-103` to `kafka-104`, the `kafka-reassign-partitions` CLI tool will first create the partitions as followers in `kafka-104`, then sync them with the leader (103), and then once it's synced switch leadership.

Given the high network delays, this operation could take an undetermined amount of time. Instead, we will take a safer route and do a reassignment in two steps: first, reassign leadership to existing, already followers, brokers, and then reassign these leadership to the new broker.

So, imagine we have the following partition assignment:

* partition 0 -> Leader: 101, Followers: 102, 103
* partition 1 -> Leader: 102, Followers: 101, 103
* partition 2 -> Leader: 103, Followers: 101, 102
* partition 3 -> Leader: 101, Followers: 102, 103
* partition 4 -> Leader: 102, Followers: 101, 103
* partition 5 -> Leader: 103, Followers: 101, 102

What we would need to do is an assignment in two steps. First, change the leaderships :

* partition 0 -> Leader: 101, Followers: 102, 103
* partition 1 -> Leader: 102, Followers: 101, 103
* partition 2 -> **Leader: 103 -> 101**, Followers: 101, 103
* partition 3 -> Leader: 101, Followers: 102, 103
* partition 4 -> Leader: 102, Followers: 101, 103
* partition 5 -> **Leader: 103 -> 101**, Followers: 102, 103

Then, we would need to tell the cluster to do reelections and use the new leaderships. This can be done using a new CLI tool:

```shell
kafka-leader-election.sh \
  --bootstrap-server kafka-101:9196 \
  --election-type preferred \
  --all-topic-partitions
```

Then, we can reassign leaderships and followers to broker 104 using a new reassignment plan.

Try to come up with the solution yourself.

Then, don't forget to turn of the faulty boker:

```shell
docker stop kafka-103 && docker rm kafka-103
```

## Solution

### Migrate leadership away from 103

Create a plan for the `perf-test` topic, named `reassignment-plan.json`.

```json
{
  "version": 1,
  "topics": [
    { "topic": "perf-test" }
  ]
}
```

Generate a plan:

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --topics-to-move-json-file reassignment-plan.json \
  --broker-list 101,102,103 \
  --generate
```

Copy the resulting json, and paste it into a new file, `reassignment.json`.

Edit the json file and update leaderships in the file: every time the first replica of a partition is the broker 103, switch it's position with either 101 or 102, so that the broker 103 is never the leader (first position) of the first replicas of every partition.

```text
        # For example for partition 3 -> switch 103 and 101
        {
            "topic": "perf-test",
            "partition": 3,
            "replicas": [
                103,           // -> switch with 101
                102,
                101            // -> switch with 103
            ],
            "log_dirs": [
                "any",
                "any",
                "any"
            ]
        }
```

Then, run the plan: 

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --reassignment-json-file reassignment.json \
  --execute
```

Verify the plan has run successfully:

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --reassignment-json-file reassignment.json \
  --verify
```

Do reelections:

```shell
kafka-leader-election.sh \
  --bootstrap-server kafka-101:9196 \
  --election-type preferred \
  --all-topic-partitions
```

Check on AKHQ that the leadership switch was effective for the `perf-test` topic.

### Move partitions from 103 to 104

Generate a new plan to move partitions from 103 to 104:

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --topics-to-move-json-file reassignment-plan.json \
  --broker-list 101,102,104 \
  --generate
```

Copy the plan and save it to the `reassignment.json` file.

Then, run the plan: 

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --reassignment-json-file reassignment.json \
  --execute
```

Verify the plan has run successfully:

```shell
kafka-reassign-partitions.sh \
  --bootstrap-server kafka-101:9196 \
  --reassignment-json-file reassignment.json \
  --verify
```

Verify in AKHQ that the `perf-test` topic doesn't contain any partition in 103.

### Stop and remove the faulty boker

```shell
docker stop kafka-103 && docker rm kafka-103
```