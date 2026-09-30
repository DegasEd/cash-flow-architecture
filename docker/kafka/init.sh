#!/bin/sh
set -e

echo "Waiting for Kafka..."

until /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:29092 \
  --list >/dev/null 2>&1
do
  sleep 2
done

echo "Creating Kafka topics..."

 /opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka:29092 \
  --create \
  --if-not-exists \
  --topic entry-events \
  --partitions 3 \
  --replication-factor 1

echo "Kafka topics ready."