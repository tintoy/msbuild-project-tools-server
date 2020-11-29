$dotnet = Get-Command 'dotnet'

& $dotnet publish "$PSScriptRoot\src\LanguageServer\LanguageServer.csproj" -f netcoreapp3.1 -o "$PSScriptRoot\out\language-server"
& $dotnet publish "$PSScriptRoot\src\LanguageServer.TaskReflection\LanguageServer.TaskReflection.csproj" -f netcoreapp3.1 -o "$PSScriptRoot\out\task-reflection"
