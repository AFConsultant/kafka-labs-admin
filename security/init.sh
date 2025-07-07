#! /bin/sh

set -o errexit
set -o nounset

WORKSPACE=/workspaces/kafka-labs-admin
CONF_DIR="$WORKSPACE/security/data"
TRUSTSTORE="$CONF_DIR/truststore.jks"
CRED="kafka-labs"

rm -rf "$CONF_DIR"/*

# Generates a self-signed Certificate Authority
# openssl req -x509 -new -keyout ca-key.pem -out ca-cert.pem -days 365 -nodes -subj "/CN=KafkaDemoCA"
openssl req -new -x509 -keyout "$CONF_DIR/ca.key" -out "$CONF_DIR/ca.crt" -days 365 -subj '/CN=KafkaLabsCA' -passin "pass:$CRED" -passout "pass:$CRED"

# Create a truststore with the CA
keytool -noprompt -keystore "$TRUSTSTORE" -alias CARoot -import -file "$CONF_DIR/ca.crt" -storepass ${CRED}

# Create a server certificate for each controller
for i in kafka-901 kafka-902 kafka-903 kafka-101 kafka-102 kafka-103
do
	echo "------------------------------- $i -------------------------------"
	WORKDIR="$CONF_DIR/$i-creds"
	KEYSTORE="$WORKDIR/$i.keystore.jks"
	CS_REQUEST="$WORKDIR/$i.csr"
	CERT_FILE="$WORKDIR/$i.crt"

    mkdir -p "$WORKDIR"
    
    # Create the controller keystore
	keytool -genkey -noprompt \
        -alias "$i" \
        -dname "CN=$i" \
        -ext "san=dns:$i" \
        -keystore "$KEYSTORE" \
        -keyalg RSA \
        -storepass ${CRED} \
        -keypass ${CRED}

	# Create the certificate signing request (CSR)
	keytool -keystore "$KEYSTORE" -alias $i -certreq -file "$CS_REQUEST" -storepass ${CRED} -keypass ${CRED}

	# Sign the host certificate with the certificate authority (CA)
	openssl x509 -req -CA "$CONF_DIR/ca.crt" -CAkey "$CONF_DIR/ca.key" -in "$CS_REQUEST" -out "$CERT_FILE" -days 365 -CAcreateserial -passin pass:${CRED}

	# Import the CA cert into the keystore
	keytool -noprompt -keystore "$KEYSTORE" -alias CARoot -import -file "$CONF_DIR/ca.crt" -storepass ${CRED}

	# Import the host certificate into the keystore
	keytool -noprompt -keystore "$KEYSTORE" -alias $i -import -file "$CERT_FILE" -storepass ${CRED} -keypass ${CRED}

	# Copy the truststore
	cp "$TRUSTSTORE" "$WORKDIR/$i.truststore.jks"
done