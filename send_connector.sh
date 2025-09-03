curl -X POST \
-H "Content-Type: application/json" \
--data '{
"name": "PerfConnector",
"config": {
"connector.class": "io.confluent.connect.jdbc.JdbcSinkConnector",
"connection.url": "jdbc:postgresql://postgres:5432/postgres",
"connection.user": "postgres",
"connection.password": "postgres",
"auto.create": "true",
"topics": "test_json2",
"value.converter": "org.apache.kafka.connect.json.JsonConverter",
    "value.converter.schemas.enable": "true",
"key.converter": "org.apache.kafka.connect.json.JsonConverter",
    "key.converter.schemas.enable": "true"
}
}' http://localhost:8083/connectors