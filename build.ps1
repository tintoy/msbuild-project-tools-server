$dotnet = Get-Command 'dotnet'

& $dotnet restore
& $dotnet publish "$PSScriptRoot\src\LanguageServer\LanguageServer.csproj" -o "$PSScriptRoot\out\language-server"
