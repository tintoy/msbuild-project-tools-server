# Building MSBuild Project Tools language server

You'll need:

1. .NET Core 2.0.0 or newer

To build:

1. `dotnet restore`
3. `dotnet publish src/LanguageServer/LanguageServer.csproj -f netcoreapp3.0 -o $PWD/out/language-server`
3. `dotnet publish src/LanguageServer.TaskReflection/LanguageServer.TaskReflection.csproj -f netcoreapp3.0 -o $PWD/out/task-reflection`

