#!/bin/bash

echo 'Building language server...'
dotnet publish src/LanguageServer/LanguageServer.csproj -f netcoreapp3.0 -o $PWD/out/language-server

echo 'Building task scanner...'
dotnet publish src/LanguageServer.TaskReflection/LanguageServer.TaskReflection.csproj -f netcoreapp3.0 -o $PWD/out/task-reflection

echo 'Done.'
