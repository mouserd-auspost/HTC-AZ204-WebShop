#!/bin/bash

dotnet build

dotnet publish -c Release

func azure functionapp publish Team03ImageResize

