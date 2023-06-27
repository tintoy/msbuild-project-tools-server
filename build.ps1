$dotnet = Get-Command 'dotnet'

& $dotnet restore
& $dotnet publish "$PSScriptRoot\src\LanguageServer\LanguageServer.csproj" -f net6.0 -o "$PSScriptRoot\out\language-server"
& $dotnet publish "$PSScriptRoot\src\LanguageServer.TaskReflection\LanguageServer.TaskReflection.csproj" -f net6.0 -o "$PSScriptRoot\out\task-reflection"
