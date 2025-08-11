# Setup

You can run the lab...

* on GitHub  (using `codespaces`)

## Running on GitHub Codespaces

### Prerequisites

* Go to the project on GitHub : https://github.com/AFConsultant/kafka-labs-admin
* Click on Code > Codespaces
* Create a new Codespace by clicking on the three dots and selecting `4-core` as Maching Type.
* You should have 30 free hours per month.

### Configuration

* Run the following scripts to setup the network and your CLI.
```sh
ansible-playbook ansible/main.yml
```
* Open a new terminal
* Test that your Kafka commands are working:
```sh
kafka-topics.sh
```
This should show the kafka-topics documentation:
```txt
➜ /workspaces/kafka-labs-admin (main) $ kafka-topics
Create, delete, describe, or change a topic.
Option                                   Description                            
------                                   -----------                            
--alter                                  Alter the number of partitions and     
                                           replica assignment. (To alter topic  
                                           configurations, the kafka-configs    
                                           tool can be used.)                   
--at-min-isr-partitions                  If set when describing topics, only    
                                           show partitions whose isr count is   
                                           equal to the configured minimum.     
--bootstrap-server <String: server to    REQUIRED: The Kafka server to connect  
  connect to>                              to.                                  
```
