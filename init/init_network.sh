#!/bin/bash

echo '127.0.0.1 kafka' | sudo tee -a /etc/hosts
echo '127.0.0.1 zookeeper' | sudo tee -a /etc/hosts
echo '127.0.0.1 ksqldb' | sudo tee -a /etc/hosts
echo '127.0.0.1 schema-registry' | sudo tee -a /etc/hosts
echo '127.0.0.1 connect' | sudo tee -a /etc/hosts
