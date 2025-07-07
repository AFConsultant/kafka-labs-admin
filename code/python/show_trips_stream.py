import requests
import json

KSQLDB_URL = "http://localhost:8088/query-stream"

SQL_STATEMENT = "SELECT * FROM trips EMIT CHANGES;"

payload = {
    "sql": SQL_STATEMENT,
    "properties": {"ksql.streams.auto.offset.reset": "earliest"}
}

headers = {
    "Content-Type": "application/vnd.ksql.v1+json"
}


print(f"Starting to stream data from the '{SQL_STATEMENT}' query...")
try:
    response = requests.post(KSQLDB_URL, headers=headers, json=payload, stream=True)
    response.raise_for_status()

    for line in response.iter_lines():
        if line:
            try:
                data = json.loads(line)
                print(data)
            except json.JSONDecodeError as e:
                print(f"Could not decode JSON line: {line}")
                print(f"Error: {e}")

except requests.exceptions.RequestException as e:
    print(f"Error connecting to ksqlDB: {e}")
except KeyboardInterrupt:
    print("\nStream closed by user.")