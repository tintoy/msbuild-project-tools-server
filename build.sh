#!/bin/bash

echo 'Restoring Nuget packages...'
dotnet restore

echo 'Building language server...'
dotnet publish src/LanguageServer/LanguageServer.csproj -o $PWD/out/language-server

echo 'Building task scanner...'
dotnet publish src/LanguageServer.TaskReflection/LanguageServer.TaskReflection.csproj -o $PWD/out/task-reflection

echo 'Done.'
