# Run ksqldb in headless mode

Add this to the docker-compose file.

```yaml
  ksqldb_headless:
    image: confluentinc/cp-ksqldb-server:7.9.1
    restart: always
    container_name: ksqldb_headless
    environment:
      KSQL_CONFIG_DIR: "/etc/ksql"
      KSQL_BOOTSTRAP_SERVERS: "kafka:9092"
      KSQL_HOST_NAME: ksqldbheadless
      KSQL_APPLICATION_ID: "ksqldb_headless"
      KSQL_KSQL_CONNECT_URL: http://connect:8083/
      KSQL_LISTENERS: "http://0.0.0.0:8088"
      KSQL_KSQL_SCHEMA_REGISTRY_URL: "http://schema-registry:8081"
      KSQL_KSQL_LOGGING_PROCESSING_STREAM_AUTO_CREATE: "true"
      KSQL_KSQL_LOGGING_PROCESSING_TOPIC_AUTO_CREATE: "true"
      KSQL_KSQL_QUERIES_FILE: "/etc/ksql/queries.sql"
    volumes:
      - ./ksqldb:/etc/ksql
    depends_on:
      kafka:
        condition: service_healthy
```

See the `ksqldb/queries.sql` file.
See that the script is executed properly.