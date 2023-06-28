$dotnet = Get-Command 'dotnet'

& $dotnet restore
& $dotnet publish "$PSScriptRoot\src\LanguageServer\LanguageServer.csproj" -o "$PSScriptRoot\out\language-server"
& $dotnet publish "$PSScriptRoot\src\LanguageServer.TaskReflection\LanguageServer.TaskReflection.csproj" -o "$PSScriptRoot\out\task-reflection"
