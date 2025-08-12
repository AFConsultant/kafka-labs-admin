# Lab 08 - Kafka Security

## Securing the transport layer

At the transport level, Kafka can either use `PLAIN` (unsecured) or `SSL`.
Each listener of a broker can usse one of these two configurations.
`PLAIN` is the default.

To setup `SSL`, the setup needs to be the following :

- Each broker needs a certificate, a keystore, and a trustore.
- Each client needs a trustore.



## Securing the authentication layer

## Securing the authorization layer
