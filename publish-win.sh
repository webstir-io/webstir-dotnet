#!/bin/bash
cd ../CLI
dotnet publish -p:AssemblyName=webstir -p:PublishSingleFile=true -p:PublishDir=../platforms/windows --self-contained true -r win-x64