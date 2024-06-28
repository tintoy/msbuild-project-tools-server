$dotnet = Get-Command 'dotnet'

& $dotnet restore
& $dotnet publish "$PSScriptRoot\src\LanguageServer\LanguageServer.csproj" -o "$PSScriptRoot\out\language-server" -f "net8.0"
& $dotnet publish "$PSScriptRoot\src\LanguageServer\LanguageServer.csproj" -o "$PSScriptRoot\out\language-server-preview" -f "net9.0"
