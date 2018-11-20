#!/bin/bash

cd /workspace/GB28181.Service
MSBuild /t:restore
dotnet clean
dotnet publish