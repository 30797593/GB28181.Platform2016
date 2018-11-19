#!/bin/bash

cd /workspace/GB28181.Service
dotnet restore
dotnet clean
dotnet publish