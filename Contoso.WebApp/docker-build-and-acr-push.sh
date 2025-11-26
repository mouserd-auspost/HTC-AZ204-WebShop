#!/bin/bash

dotnet build

dotnet publish -c Release

az login

az acr login -n team03registry

docker build -t contoso-webapp:latest .

docker tag contoso-webapp:latest team03registry.azurecr.io/contoso-webapp:latest

docker push team03registry.azurecr.io/contoso-webapp:latest

