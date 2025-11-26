#!/bin/bash

dotnet build

dotnet publish -c Release

az login

az acr login -n team03registry

docker build -t contoso-api:latest .

docker tag contoso-api:latest team03registry.azurecr.io/contoso-api:latest

docker push team03registry.azurecr.io/contoso-api:latest
