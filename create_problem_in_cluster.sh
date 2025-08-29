docker-compose exec netem-103 tc qdisc add dev eth0 root netem delay 1000ms 10000ms
