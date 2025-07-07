#!/bin/bash

BASHRC="$HOME/.bashrc"
MARKER="# Kafka CLI commands"

KAFKA_COMMANDS=$(cat <<EOF
$MARKER
kafka-topics() {
  docker exec -it kafka /opt/kafka/bin/kafka-topics.sh "\$@"
}

kafka-console-producer() {
  docker exec -it kafka /opt/kafka/bin/kafka-console-producer.sh "\$@"
}

kafka-console-consumer() {
  docker exec -it kafka /opt/kafka/bin/kafka-console-consumer.sh "\$@"
}

kafka-consumer-groups() {
  docker exec -it kafka /opt/kafka/bin/kafka-consumer-groups.sh "\$@"
}

ksql() {
  docker exec -it ksqldb ksql "\$@"
}
EOF
)

# Add commands to .bashrc if not already present
if ! grep -q "$MARKER" "$BASHRC"; then
  echo "$KAFKA_COMMANDS" >> "$BASHRC"
  echo "Kafka commands added to .bashrc."
else
  echo "Kafka commands already present in .bashrc."
fi

# Reload .bashrc
source "$BASHRC"
