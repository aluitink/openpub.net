#!/bin/bash
set -e

cd /src/SampleProjects/DemoApp
dotnet build -c Release
dotnet publish -c Release -o /app/publish
