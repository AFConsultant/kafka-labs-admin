# Lab 07 - Capacity planning

## Broker capacity planning

The different elements that impact the capacity planning :

- Disk space and I/O
- Network bandwidth
- RAM (for page cache)
- CPU

To calculate the estimate number of brokers needed in your cluster :

### Number of brokers needed

Number of brokers = (messages per day * message size * Retention days * Replication) / (disk space per Broker)

Number of brokers = (messages per sec * message size * Number of Consumers) / (network bandwidth per Broker)

These are usually back of the enveloppe calculation and managing a cluster is also about seeing the current load of the cluster.

### Monitoring the capacity limits

#### Disk space capacity

Open **Grafana** → Dashboard **Kafka / Hard Disk Usage**.
Confluent recommend Alerting at 60% capacity.

Query :
sum(kafka_log_Size) by (service) / (1024^3)

## Alerts

kafka.server:type=ReplicaManager,name=UnderReplicatedPartitions
Alert if the value is greater than 0 for a long time

Alert on max consumer group lag across all topics.

• Open file handles
◦ Set ulimit -n 100000
◦ Alert at 60% of the limit


• Disk
◦ Alert at 60% capacity


• Network bytesIn/bytesOut
◦ Alert at 60% capacity


Thread capacity
kafka.server:type=KafkaRequestHandlerPool,name=RequestHandlerAvgIdlePercent (meter)
kafka.network:type=SocketServer,name=NetworkProcessorAvgIdlePercent (gauge)
◦ 0 indicates all resources are used
◦ 1 indicates all resources are available
Alert if the value drops below 0.4
◦ If it does, consider increasing the number of threads
