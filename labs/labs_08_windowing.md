# Preparation

Create an `orders` topic.
```sh
kafka-topics --bootstrap-server kafka:9092 --create --topic orders --partitions 3 --replication-factor 1 ;
```

# Order events

We are going to receive these JSON events in the order topic.

Order 101 at 00:05 - `{"ts": "2025-06-12T17:00:05Z"}`

Order 102 at 00:10 - `{"ts": "2025-06-12T17:00:10Z"}`

Order 103 at 00:15 - `{"ts": "2025-06-12T17:00:15Z"}`

Order 104 at 00:29 - `{"ts": "2025-06-12T17:00:29Z"}`

Order 105 at 00:30 - `{"ts": "2025-06-12T17:00:30Z"}`

Our goal is to count those orders by a time window of 30 seconds.

This lab will demonstrate that orders are aggregated together when they arrive.

# ksqlDB setup

First, **before sending any events**, we are going to create the ksql aggregation.

Connect to ksqldb:
```sh
ksql localhost:8088
```

Create the orders stream:
```sql
CREATE STREAM orders (
    order_id VARCHAR KEY,
    ts VARCHAR
) WITH (
    KAFKA_TOPIC = 'orders',
    VALUE_FORMAT = 'JSON',
    TIMESTAMP = 'ts',
    TIMESTAMP_FORMAT = 'yyyy-MM-dd''T''HH:mm:ss''Z'''
);
```
Note that we use the event time instead of the ingestion time.

Create the aggregation:
```sql
CREATE TABLE c AS
SELECT
    1,
    COUNT(*) AS order_count
FROM orders
WINDOW TUMBLING (SIZE 30 SECONDS)
GROUP BY 1;
```

Now, print messages in the ORDERS_COUNT topic. Keep it running.
```sql
PRINT ORDERS_COUNT;
```

# Sending the events

In another terminal, let's send our first event Order 101 at 00:05.
We will verify that the event is received in the counting topic.

```sh
echo '101:{"ts": "2025-06-12T17:00:05Z"}' | kcat -b kafka:9092 -t orders -K:
```
We can confirm that the order was received and indeed processed.
```sh
ksql> PRINT ORDERS_COUNT;
Key format: HOPPING(KAFKA_INT) or TUMBLING(KAFKA_INT) or HOPPING(KAFKA_STRING) or TUMBLING(KAFKA_STRING)
Value format: JSON or KAFKA_STRING
rowtime: 2025/06/12 17:00:05.000 Z, key: [1@1749747600000/-], value: {"ORDER_COUNT":1}, partition: 0
```
We can see that the event has the start of it's window in the key, which is 1749747600000. Using [epoch converter](https://www.epochconverter.com/), we see that the starting window is `7:00:00 PM.`.
Since we set a 30 seconds window, if we send other events before `7:00:00 PM`, they should arrive in the same window. Let's test this by sending our second event Order 102 at 00:10.

```sh
echo '102:{"ts": "2025-06-12T17:00:10Z"}' | kcat -b kafka:9092 -t orders -K:
```

When receiving this event, we see that a new message has been received in the `ORDERS_COUNT` topic, but with the same key and an updated count:
```sh
rowtime: 2025/06/12 17:00:10.000 Z, key: [1@1749747600000/-], value: {"ORDER_COUNT":2}, partition: 0
```

Order 103 at 00:15 and Order 104 at 00:29 should yield the same result.

```sh
echo '103:{"ts": "2025-06-12T17:00:15Z"}' | kcat -b kafka:9092 -t orders -K:
echo '104:{"ts": "2025-06-12T17:00:29Z"}' | kcat -b kafka:9092 -t orders -K:
```

And, indeed, the count is updated: 
```sh
rowtime: 2025/06/12 17:00:29.000 Z, key: [1@1749747600000/-], value: {"ORDER_COUNT":4}, partition: 0
```

But what about Order 105 at 00:30 ? Let's test this.

```sh
echo '105:{"ts": "2025-06-12T17:00:30Z"}' | kcat -b kafka:9092 -t orders -K:
```

The result is telling :
```sh
rowtime: 2025/06/12 17:00:30.000 Z, key: [1@1749747630000/-], value: {"ORDER_COUNT":1}, partition: 0
```
The count is back to one ! We can see that the key of the event changed by 30_000 ms, which is indeed the next 30 seconds time window `7:00:30 PM`.

# Lab Part 2: Emitting Final Results with EMIT FINAL

In this part of the lab, we'll modify our aggregation to only emit a single, final count after each 30-second window is complete. This is ideal for downstream consumers that only care about the final result of a calculation and don't need to process every intermediate update.

The key to this is the EMIT FINAL clause, which we will add to our CREATE TABLE statement.

## Clean Up Previous Objects
First, let's stop the PRINT command by pressing <CTRL+C> in the ksqlDB CLI.

Next, we need to drop the table ORDERS_COUNT that we created in the first part of the lab. This will also automatically delete its underlying Kafka topic (ORDERS_COUNT).

```sql
DROP TABLE ORDERS_COUNT;
DROP STREAM ORDERS;
```

Now, recreate the source topic to ensure we start with a clean slate. Run these commands in your shell (not in the ksqlDB CLI):
```sh
kafka-topics --bootstrap-server kafka:9092 --delete --topic orders ;
kafka-topics --bootstrap-server kafka:9092 --delete --topic ORDERS_COUNT ;
kafka-topics --bootstrap-server kafka:9092 --create --topic orders --partitions 3 --replication-factor 1 ;
```

Recreate the orders stream in ksqlDB:
```sql
CREATE STREAM orders (
    order_id VARCHAR KEY,
    ts VARCHAR
) WITH (
    KAFKA_TOPIC = 'orders',
    VALUE_FORMAT = 'JSON',
    TIMESTAMP = 'ts',
    TIMESTAMP_FORMAT = 'yyyy-MM-dd''T''HH:mm:ss''Z'''
);
```
## Create the Final Aggregation
Now, we will recreate the aggregation, but this time we'll add the   `EMIT FINAL` clause. This instructs ksqlDB to output the result for a given window only once, after the window has closed. A window is considered closed when the event time of the stream (as determined by the ts field) passes the window's end boundary.

Let's create the new table. We'll call it orders_final_count.
```sql
CREATE TABLE orders_final_count AS
SELECT
    1 as key,
    COUNT(*) AS order_count
FROM orders
WINDOW TUMBLING (SIZE 30 SECONDS)
GROUP BY 1
EMIT FINAL;
```

Now, let's print the messages from the underlying topic for our new table, which by default will be named `ORDERS_FINAL_COUNT`. Keep this running so we can observe the results.

```sql
PRINT ORDERS_FINAL_COUNT;
```

Note: The command above prints the table itself, which automatically reads from the underlying topic.

At this point, you should see no output, as no events have been sent yet.

## Sending the Events (Again)
In your other terminal, let's send the same series of events.

Send Orders 101, 102, 103, and 104 (all within the first 30-second window):

```sh
echo '101:{"ts": "2025-06-12T17:00:05Z"}' | kcat -b kafka:9092 -t orders -K:
echo '102:{"ts": "2025-06-12T17:00:10Z"}' | kcat -b kafka:9092 -t orders -K:
echo '103:{"ts": "2025-06-12T17:00:15Z"}' | kcat -b kafka:9092 -t orders -K:
echo '104:{"ts": "2025-06-12T17:00:29Z"}' | kcat -b kafka:9092 -t orders -K:
```

Look at your ksqlDB PRINT terminal. You will notice that nothing has been printed. This is the `EMIT FINAL` clause at work. ksqlDB has processed these events and is maintaining the count internally, but it's waiting for the window to close before emitting the result.

The window for these events is 2025-06-12T17:00:00Z to 2025-06-12T17:00:30Z (excluding 00:30).

### Send Order 105 (in the next window)

Now, let's send the event that will close the first window. The timestamp 17:00:30Z falls into the next window, so its arrival signals to ksqlDB that the first window (...:00 to ...:30) is now finished.

```sh
echo '105:{"ts": "2025-06-12T17:00:30Z"}' | kcat -b kafka:9092 -t orders -K:
```

## Analyzing the Final Result
Check your `PRINT` terminal now. You will see a single message has finally appeared:

```sh
Key format: HOPPING(KAFKA_INT) or TUMBLING(KAFKA_INT) or HOPPING(KAFKA_STRING) or TUMBLING(KAFKA_STRING)
Value format: JSON or KAFKA_STRING
rowtime: 2025/06/12 17:00:29.000 Z, key: [1@1749747600000/-], value: {"ORDER_COUNT":4}, partition: 0
```

## Warning with EMIT FINAL

What happens if the Order 105 never arrives ? 
The window never closes. As you can imagine, this can be problematic...

In real-world systems, this is often handled by having sources that produce periodic "heartbeat" messages to advance the stream time even if there's no new business data.