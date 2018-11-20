#!/bin/bash

cd /workspace/GB28181.Service
MSBuild /t:Restore GB28181.Platform.Service.sln
dotnet clean
dotnet publish