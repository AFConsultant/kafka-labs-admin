#!/usr/bin/env python3
"""
Send station-info CSV rows to a Kafka topic.

Needs the `confluent-kafka` pip package to be installed.
```sh
sudo apt install python3-confluent-kafka # if running in codespace
pip install confluent-kafka              # otherwis
```

Usage:
    python publish_stations.py stations.csv
"""

import sys
from confluent_kafka import Producer


# ---------- Kafka configuration ----------
KAFKA_BOOTSTRAP_SERVERS = "kafka:9092"
TOPIC_NAME = "station_infos_raw_csv"
# -----------------------------------------


def delivery_report(err, msg):
    """Called for each message to indicate delivery result."""
    if err is not None:
        print(f"❌ Delivery failed for key={msg.key().decode()}: {err}")
    # else:
    #     print(f"✅ Delivered: {msg.key().decode()} → partition {msg.partition()}")


def main(csv_path: str) -> None:
    # Configure and create the producer
    producer = Producer({"bootstrap.servers": KAFKA_BOOTSTRAP_SERVERS})

    with open(csv_path, "r", encoding="utf-8") as f:
        # Skip header
        header = next(f, None)

        # Publish each row
        for line in f:
            line = line.rstrip("\n")                # remove newline
            if not line:
                continue

            station_id = line.split(",", 1)[0]      # first field is the key
            producer.produce(
                topic=TOPIC_NAME,
                key=station_id,
                value=line,
                callback=delivery_report,
            )

    # Wait for all messages to be delivered
    producer.flush()
    print("✅ All rows sent.")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python publish_stations.py <path/to/stations.csv>")
        sys.exit(1)
    main(sys.argv[1])
 