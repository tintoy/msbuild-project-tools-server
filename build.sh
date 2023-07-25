#!/bin/bash

echo 'Restoring Nuget packages...'
dotnet restore

echo 'Building language server...'
dotnet publish src/LanguageServer/LanguageServer.csproj -o $PWD/out/language-server

echo 'Done.'
