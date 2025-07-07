# Setup

You can run the lab...

* on your local machine
* on your local machine (using `devcontainers`)
* on GitHub  (using `devcontainers`)

## Running on GitHub Codespaces

### Prerequisites

* Go to the project on GitHub : https://github.com/AFConsultant/kafka-labs
* Click on Code > Codespaces
* Create a new Codespace by clicking on the three dots and selecting `4-core` as Maching Type.
* You should have 30 free hours per month.

### Configuration

* Run the following scripts to setup the network and your CLI.
```sh
sudo ./init/init_network.sh
./init/init_cli.sh
source ~/.bashrc
```
* Test that your Kafka commands are working:
```sh
kafka-topics
```
This should show the kafka-topics documentation:
```txt
➜ /workspaces/kafka-labs (main) $ kafka-topics
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

## Running on your local machine

### Prerequisites

* Have docker and docker-compose installed.
* Have at least 8 GB or RAM and 4 CPU.

### Clone the project

```sh
git clone git@github.com:AFConsultant/kafka-labs.git
```

### Configuration

Add the [kafka hosts](/init/init_network.sh) to your /etc/hosts.
Add the [CLI tools](/init/init_cli.sh) to your `.bashrc` or `.zshrc`. 
If you are on Windows, I advise to run WSL.
Unix scripts in /init/ are available. Run the init_network.sh with sudo.
You can also use ansible : `ansible-playbook ansible/init.yml`.