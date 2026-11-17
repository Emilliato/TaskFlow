# Episode 5.3 demos: load balancing, a message queue, gRPC

Three small, stand-alone demos. They are not part of `TaskFlow.slnx`, they change no TaskFlow code, and `dotnet test` is unaffected.
Everything runs locally. Nothing here uses a cloud account. Docker is needed for the first two.
The passwords and keys below are fake demo values.

## 1. Load balancing: nginx in front of two TaskFlow containers

```bash
docker build -t taskflow:5.3 .                              # from the repository root
docker network create --subnet 10.53.0.0/24 tf-lb-net       # fixed addresses, so nginx can name the instance
docker run -d --name tf-app1 --network tf-lb-net --ip 10.53.0.11 -e TaskFlow__AdminApiKey=demo-FAKE-key-0123456789 taskflow:5.3
docker run -d --name tf-app2 --network tf-lb-net --ip 10.53.0.12 -e TaskFlow__AdminApiKey=demo-FAKE-key-0123456789 taskflow:5.3
docker create --name tf-lb --network tf-lb-net -p 5731:80 nginx:1.29-alpine
docker cp tools/microservices-demo/lb/nginx.conf tf-lb:/etc/nginx/nginx.conf
docker start tf-lb
curl -i http://localhost:5731/tasks        # the X-Served-By header says app1 or app2
```

Each instance has its own SQLite file, so data created through one instance is invisible to the other.
That is the point of the demo, not a bug. Scale out with `tf-app3` and `lb/nginx-3.conf` (`docker exec tf-lb nginx -s reload`).
Clean up: `docker rm -f tf-lb tf-app1 tf-app2 tf-app3 && docker network rm tf-lb-net`.

## 2. A message queue: RabbitMQ and a producer / consumer

```bash
docker run -d --name tf-rmq -p 127.0.0.1:5743:5672 -e RABBITMQ_DEFAULT_USER=demo -e RABBITMQ_DEFAULT_PASS=demo-FAKE-pass-0123 rabbitmq:4-alpine
export RABBIT_PORT=5743 RABBIT_USER=demo RABBIT_PASSWORD=demo-FAKE-pass-0123
dotnet run --project tools/microservices-demo/queue -- inline     # five 2 second jobs, one after another
dotnet run --project tools/microservices-demo/queue -- publish    # the same five, put on a queue
dotnet run --project tools/microservices-demo/queue -- consume    # a separate process does the work
```

Run `rabbitmqctl` inside the container as the `rabbitmq` user (`docker exec -u rabbitmq tf-rmq rabbitmqctl ...`).
Run as root, the first `docker exec` creates the Erlang cookie file with the wrong owner and the broker then crashes at start.

## 3. gRPC next to REST

```bash
dotnet run --project tools/microservices-demo/grpc -- server    # gRPC on 5721, plain JSON REST on 5722
dotnet run --project tools/microservices-demo/grpc -- client    # in a second terminal
```

`seams.sh` prints the project references and the application-layer interfaces: the places where a network boundary could go if TaskFlow were ever split.
