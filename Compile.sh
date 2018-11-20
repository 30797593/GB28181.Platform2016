#!/bin/bash

MSBuild /t:Restore GB28181.Platform.Service.sln
cd /workspace/GB28181.Service
dotnet clean
dotnet publish