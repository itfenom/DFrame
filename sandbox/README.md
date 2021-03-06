## Getting Started

Use ConsoleApp to try DFrame with InProcessScalingProvider and LoadTest to HTTP(S) Server.

**Visual Studio**

Open DFrame.sln and launch EchoServer then ConsoleApp.

> If you are using [SwitchStartupProject for VS2019](https://heptapod.host/thirteen/switchstartupproject) use `ConsoleApp + EchoServer`.

**dotnet cli**

run echo server.

```shell
docker run -it --rm -p 5000:80 cysharp/dframe-echoserver:latest
```

run sample ConsoleApp.

```shell
dotnet run --project sandbox/ConsoleApp
```

## WebApp

WebApp provide WebUI and record Profiler History to database.
Use EntityFramework to use database.

```shell
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

add migrations.

```shell
dotnet ef migrations add docker
```

run migrations.

```shell
docker-compose -f sandbox/docker-compose.yaml up
```

## docker

Try inprocess or Out of Process (oop).

```shell
docker run -it cysharp/dframe_sample_oop
```

memo for build & push.

```shell
docker build -t dframe_sample_oop:0.1.0 -f sandbox/ConsoleApp/Dockerfile .
docker tag dframe_sample_oop:0.1.0 cysharp/dframe_sample_oop
docker push cysharp/dframe_sample_oop
```

## Kubernetes Scaling Provider (k8s)

You can deploy DFrame to your Kubernetes cluster and run load test.
This sample contains Kustomize based kubernetes deployment.

### rbac-less kubernetes

following is rbac-less cluster.

```shell
kubectl apply -f sandbox/k8s/dframe/overlays/local/namespace.yaml
kubens dframe
# Generate ImagePullSecret if you host image on your private registry like ECR.
kubectl delete secret aws-registry
kubectl create secret docker-registry aws-registry \
              --docker-server=https://<ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com \
              --docker-username=AWS \
              --docker-password=$(aws ecr get-login-password) \
              --docker-email=no@email.local
kubectl delete job dframe-master
kubectl kustomize sandbox/k8s/dframe/overlays/local | kubectl apply -f -
stern dframe*
```

### RBAC Kubernetes

enable ServiceAccount and Roles to run on RBAC cluster.

```shell
kubectl kustomize sandbox/k8s/dframe/overlays/development | kubectl apply -f -
kubens dframe
stern dframe*

kubectl kustomize sandbox/k8s/dframe/overlays/development | kubectl delete -f -
```

### EKS NodeGroup

This example run on ESK with NodeGroup named `dframe`.

```shell
kubectl kustomize sandbox/k8s/dframe/overlays/development-nodegroup | kubectl apply -f -
kubens dframe
stern dframe*

kubectl kustomize sandbox/k8s/dframe/overlays/development-nodegroup | kubectl delete -f -
```

### EKS Fargate

This example run on EKS with Fargate, fargate profile is enable to `dframe-fargate` namespace label.
Make sure Fargate pod is slow to start, it takes 30sec to 150sec until Ready state. 
You may wait about 3min until Fargates start your DFrame Worker.


```shell
kubectl kustomize sandbox/k8s/dframe/overlays/development-fargate | kubectl apply -f -
kubens dframe-fargate
stern dframe*

kubectl kustomize sandbox/k8s/dframe/overlays/development-fargate | kubectl delete -f -
```

```shell
kubectl run -it --rm --restart=Never -n dframe-fargate --image=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s:0.1.0 --image-pull-policy Always --env DFRAME_MASTER_CONNECT_TO_HOST=dframe-master.dframe-fargate.svc.cluster.local --env DFRAME_WORKER_IMAGE_NAME=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s --env DFRAME_WORKER_IMAGE_TAG="0.1.0" --env DFRAME_WORKER_IMAGE_PULL_POLICY="Always" --serviceaccount='dframe-master' --port=12345 --labels="app=dframe-master" dframe-master -- "request" "-processCount" "1" "-workerPerProcess" "10" "-executePerWorker" "10" "-workerName" "SampleWorker"
```

### Redeploy

If you already deployed service and rbac resources, service account and others, you can try fast load testing itelation by just change args and run  DFrame master as pod.
Below sample will run 1000000 requests of SampleHttpWorker, includes 10 process 10 workers and 10000 execute.

```shell
kubectl run -it --rm --restart=Never -n dframe --image=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s:0.1.0 --image-pull-policy Always --env DFRAME_MASTER_CONNECT_TO_HOST=dframe-master.dframe.svc.cluster.local --env DFRAME_WORKER_IMAGE_NAME=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s --env DFRAME_WORKER_IMAGE_TAG="0.1.0" --env DFRAME_WORKER_IMAGE_PULL_POLICY="Always" --serviceaccount='dframe-master' --port=12345 --labels="app=dframe-master" dframe-master -- "batch -processCount" "10" "-workerPerProcess" "10" "-executePerWorker" "10000" "-workerName" "SampleHttpWorker"
```

You can try LoadTest to MagicOnion with SampleUnaryWorker and SampleStreamWorker.

```shell
kubectl run -it --rm --restart=Never -n dframe --image=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s:0.1.0 --image-pull-policy Always --env DFRAME_MASTER_CONNECT_TO_HOST=dframe-master.dframe.svc.cluster.local --env DFRAME_WORKER_IMAGE_NAME=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s --env DFRAME_WORKER_IMAGE_TAG="0.1.0" --env DFRAME_WORKER_IMAGE_PULL_POLICY="Always" --serviceaccount='dframe-master' --port=12345 --labels="app=dframe-master" dframe-master -- "batch -processCount" "10" "-workerPerProcess" "10" "-executePerWorker" "10000" "-workerName" "SampleUnaryWorker"
```

```shell
kubectl run -it --rm --restart=Never -n dframe --image=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s:0.1.0 --image-pull-policy Always --env DFRAME_MASTER_CONNECT_TO_HOST=dframe-master.dframe.svc.cluster.local --env DFRAME_WORKER_IMAGE_NAME=431046970529.dkr.ecr.ap-northeast-1.amazonaws.com/dframe-k8s --env DFRAME_WORKER_IMAGE_TAG="0.1.0" --env DFRAME_WORKER_IMAGE_PULL_POLICY="Always" --serviceaccount='dframe-master' --port=12345 --labels="app=dframe-master" dframe-master -- "batch -processCount" "10" "-workerPerProcess" "10" "-executePerWorker" "10000" "-workerName" "SampleStreamWorker"
```

## etc....

### ab test on k8s

```shell
# 10並列 / 10000 リクエスト
kubectl run -i --rm --restart=Never -n dframe --image=mocoso/apachebench apachebench -- bash -c "ab -n 10000 -c 10 http://77948c50-apiserver-apiserv-98d9-538745285.ap-northeast-1.elb.amazonaws.com/healthz"
```

### loadtest target server http

build

```shell
docker build -t cysharp/dframe-echoserver:0.0.1 -f sandbox/EchoServer/Dockerfile .
docker tag cysharp/dframe-echoserver:0.0.1 cysharp/dframe-echoserver:latest
docker push cysharp/dframe-echoserver:0.0.1
docker push cysharp/dframe-echoserver:latest
```

let's launch apiserver to try httpclient access bench through dframe worker.

local

```shell
kubectl kustomize sandbox/k8s/apiserver/overlays/local | kubectl apply -f -
kubens apiserver
curl http://localhost:5000
```

aws

```shell
kubectl kustomize sandbox/k8s/apiserver/overlays/aws | kubectl apply -f -
kubens apiserver
curl "http://$(kubectl get ingress -o jsonpath='{.items[].status.loadBalancer.ingress[].hostname}')"
```

remove kubernetes resource.

```shell
kubectl kustomize sandbox/k8s/apiserver/overlays/aws | kubectl delete -f -
```

### loadtest target server grpc

build

```shell
docker build -t cysharp/dframe-magiconion:0.0.1 -f sandbox/EchoMagicOnion/Dockerfile .
docker tag cysharp/dframe-magiconion:0.0.1 cysharp/dframe-magiconion:latest
docker push cysharp/dframe-magiconion:0.0.1
docker push cysharp/dframe-magiconion:latest
```

let's launch magiconion to try magiconion access bench through dframe worker.

local

```shell
kubectl kustomize sandbox/k8s/apiserver/overlays/local | kubectl apply -f -
kubens apiserver
echo localhost:12346
```

aws

```shell
kubectl kustomize sandbox/k8s/magiconionserver/overlays/aws | kubectl apply -f -
kubens apiserver
echo "$(kubectl get service magiconion -o jsonpath='{.status.loadBalancer.ingress[].hostname}'):12346"
```

remove kubernetes resource.

```shell
kubectl kustomize sandbox/k8s/magiconionserver/overlays/aws | kubectl delete -f -
```
