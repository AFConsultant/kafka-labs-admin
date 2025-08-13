# Lab 08 - Kafka Security

## Securing the transport layer

At the transport level, Kafka can either use `PLAIN` (unsecured) or `SSL`.
Each listener of a broker can usse one of these two configurations.
`PLAIN` is the default.

To setup `SSL`, the setup needs to be the following :

- Each broker needs a certificate, a keystore, and a trustore.
- Each client needs a trustore.

See the `security/init.sh` script. This scripts creates a self-signed CA, and then for each broker the necessary elements. This is then mounted as a volume in each broker that uses it.

Check the `docke-compose.yaml` file : each broker has it's keystore and truststore setup (`KAFKA_SSL_KEYSTORE_LOCATION`, `KAFKA_SSL_TRUSTSTORE_LOCATION`).
The `KAFKA_LISTENER_SECURITY_PROTOCOL_MAP` defines the SSL communication. On Service Brokers, SSL is active on the following listeners: `CONTROLLER`, `REPLICATION`, `INTERNAL`, `EXTERNAL`.

## Securing the authentication layer

Clients can authenticate to Kafka using several methods. The most common methods are mutual SSL, SASL and Kerberos.
Mutual SSL means that client use their certificate to connect to the cluster. To setup this, each client needs it's own certificate, and the truststore of the brokers needs to contain the CA used to generate those certificates. This works great but requires to keep track of certificates expiration dates.

SASL is a username / password system. It exists in two variations: PLAIN and SCRAM.

- PLAIN means that the users are static and the passwords are exchanged in plaintext.
- SCRAM means that the users are dynamic and the passwords are salted.

In this lab, we will use SASL with those two configurations.

### Setting up the SASL

All Brokers (service broker and controllers) needs to communicate between themselves with either service requests (FetchFollower) or metadata requests (leader elections). This communication needs to be properly secured and authentified. The simplest solution is to declare static users with `SASL/PLAIN`, and configure them as static users.

Clients don't need static users and are perfectly fine using `SASL/SCRAM`.

On each broker, the `SASL` setup looks like this:

- `KAFKA_SASL_ENABLED_MECHANISMS` to enabled `PLAIN` and `SCRAM-SHA-512`
- `KAFKA_LISTENER_SECURITY_PROTOCOL_MAP` to map `SASL` to the following listeners: `CONTROLLER`,`REPLICATION`,`INTERNAL`,`EXTERNAL`
- `KAFKA_SASL_MECHANISM_CONTROLLER_PROTOCOL` to allow `SASL/PLAIN` when communicating between brokers <-> controllers
- `KAFKA_SASL_MECHANISM_INTER_BROKER_PROTOCOL` to allow `SASL/PLAIN` when communicating between service brokers
- `KAFKA_CONTROLLER_LISTENER_NAMES` to use the `CONTROLLER` listener when communicating between brokers <-> controllers
- `KAFKA_INTER_BROKER_LISTENER_NAME` to use the `REPLICATION` listener when communicating between brokers <-> controllers
- `KAFKA_SUPER_USERS` to define users with admin privileges.

On controller brokers only, the following needs to be configured:

- `KAFKA_LISTENER_NAME_CONTROLLER_PLAIN_SASL_JAAS_CONFIG`: This configures the JAAS module that allows `SASL/PLAIN` to works, with the user of the broker and the static list of known users. This list will only be used for the `CONTROLLER` listeners of the controller brokers. This means that a service broker can't connect to another service broker using users in this list (since service brokers don't have `SASL/PLAIN` listeners configured).

On service brokers only, the following needs to be configured:

- `KAFKA_OPTS`: this contains the JAAS `SASL/SCRAM` configuration for the service brokers -> `"-Djava.security.auth.login.config=/opt/kafka/config/jaas/broker.conf"`. Users are statically configured for the brokers, but the client are dynamically configured.

### Creating a users

The service `create-users` in `docker-compose.yaml` creates `SASL/SCRAM` users. This is done using the `kafka-configs` CLI tool.

Let's create a new user, John:

```shell
kafka-configs.sh \
  --bootstrap-server kafka-101:9196,kafka-102:9296,kafka-103:9396 \
  --alter \
  --add-config 'SCRAM-SHA-512=[iterations=8192,password=john-secret]' \
  --entity-type users \
  --entity-name john ;
```

Now, we will use this user to connect to Kafka. Note that only one port is exposed from Docker for the service brokers, the `HOST:PLAINTEXT` listener. This means that we won't be able to authenticate on this port.
We need to connect to a broker to access either the `INTERNAL` (`SASL_PLAINTEXT`) or `EXTERNAL` (`SASL_SSL`) listeners which are client ports.

```shell
docker exec -it kafka-101 bash
cd /opt/kafka
```

First, let's try to connect to the secured `EXTERNAL:SASL_SSL` listener (port 9095) without any user.

```shell
/opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --list ;
```

We get a `OutOfMemoryError` because the server expects a SSL connection on that port.
Let's create a client configuration file that we will use to authenticate.
The configuration files is a property file containing the following properties:

```text
security.protocol=SASL_SSL
sasl.mechanism=SCRAM-SHA-512
sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required \
   username="john" \
   password="john-secret";
ssl.truststore.location=/opt/kafka/security/kafka-101.truststore.jks
ssl.truststore.password=kafka-labs
```

Create this file:

```shell
cat > john.properties << 'EOF'
security.protocol=SASL_SSL
sasl.mechanism=SCRAM-SHA-512
sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required \
   username="john" \
   password="john-secret";
ssl.truststore.location=/opt/kafka/security/kafka-101.truststore.jks
ssl.truststore.password=kafka-labs
EOF
```

Let's retry to connect to the cluster using the John user.

```shell
/opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --command-config john.properties \
  --list ;
```

You should see, in the middle of the logs:

```text
perf-test
```

The client successfully connected using SSL and SASL to the cluster !

## Securing the authorization layer

Kafka uses ACL (Access Control Lists) to handle authorization.
ACL is active in our cluster, but with an allow-by-default configuration.

Let's see the configurations:

- `KAFKA_AUTHORIZER_CLASS_NAME`: this configures Kafka default `StandardAuthorizer`
- `KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND`: this is the `allow-by-default` (see the .env file)

John, by default, has every accesses on the cluster and can, for example, create topics:

```shell
# Still connected to the kafka-101
/opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --command-config john.properties \
  --create \
  --topic "john-topic" \
  --replication-factor 3 \
  --partitions 3 ;
```

Of course, we don't want non super users to create topics. So we will deactivate the `KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND` configuration, setting it to false. Go ahead and modify your `.env` file accordingly.

Shut down your brokers - without removing your volumes (don't forget to disconnect from kafka-101 first).

```shell
docker-compose down
```

Then restart your environment:

```shell
docker-compose up -d
```

Connect to kafka-101:

```shell
docker exec -it kafka-101 bash
```

Recreate the "jon.properties" file:

```shell
cd /opt/kafka
cat > john.properties << 'EOF'
security.protocol=SASL_SSL
sasl.mechanism=SCRAM-SHA-512
sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required \
   username="john" \
   password="john-secret";
ssl.truststore.location=/opt/kafka/security/kafka-101.truststore.jks
ssl.truststore.password=kafka-labs
EOF
```

And then try to list the topics again:

```shell
/opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --command-config john.properties \
  --list ;
```

Nothing is returned. Let's try to create another topic:

```shell
/opt/kafka/bin/kafka-topics.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --command-config john.properties \
  --create \
  --topic "john-topic-2" \
  --replication-factor 3 \
  --partitions 3 ;
```

We get an error:

```text
Error while executing topic command : Authorization failed.
[main] ERROR org.apache.kafka.tools.TopicCommand - org.apache.kafka.common.errors.TopicAuthorizationException: Authorization failed.
```

Let's try to producer to the topic we created earlier, `john-topic`:

```shell
/opt/kafka/bin/kafka-console-producer.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --producer.config john.properties \
  --topic "john-topic" ;
```

We get authorization errors once again.
Let's give ALC to allow John to write to this topic.

To do this, we will use the `admin` user, which has been declared as a super user inside the `docker-compose.yaml` file.
We will use the Kafka ACL CLI tool.

```shell
/opt/kafka/bin/kafka-acls.sh \
  --bootstrap-server kafka-101:9094 \
  --add \
  --allow-principal User:john \
  --operation Write \
  --topic "john-topic" \
  --command-config /opt/kafka/clients/internal-admin.properties;
```

You should receive a confirmation message like this:

```text
Adding ACLs for resource `ResourcePattern(resourceType=TOPIC, name=john-topic, patternType=LITERAL)`: 
        (principal=User:john, host=*, operation=WRITE, permissionType=ALLOW)
```

Let's retry to publish messages (don't pay attention to the extra logs):

```shell
/opt/kafka/bin/kafka-console-producer.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --producer.config john.properties \
  --topic "john-topic" ;
```

Check in AKHQ that the messages have been received in this topic.
It's now time to read these messages. Let's add a new Read ACL for John:

```shell
/opt/kafka/bin/kafka-acls.sh \
  --bootstrap-server kafka-101:9094 \
  --add \
  --allow-principal User:john \
  --consumer \
  --topic "john-topic" \
  --group "john-consumer-group" \
  --command-config /opt/kafka/clients/internal-admin.properties;
```

Let's consume these messages:

```shell
/opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server kafka-101:9095,kafka-102:9095,kafka-103:9095 \
  --consumer.config john.properties \
  --topic "john-topic" \
  --group "john-consumer-group" \
  --from-beginning ;
```

You should see the messages you sent previously.