#!/bin/bash

dotnet build

dotnet publish -c Release

cd bin/Release/net8.0/publish

zip -r ../publish.zip .

cd ..

az webapp deploy --resource-group team03 --name APITeam03 --src-path ./publish.zip --type zip

cd ../../../


