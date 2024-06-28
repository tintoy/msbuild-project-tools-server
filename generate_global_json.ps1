# Run this script to update global.json
# I don't know how to write bash script;; (from. nemonuri)

try {
    # Value of 'dotnet --version' is changed by global.json
    $globalJsonPath = ".\global.json"

    # so, delete first.
    if ([System.IO.File]::Exists($globalJsonPath)) {
        [System.IO.File]::Delete($globalJsonPath)
    }

    $dotnet = Get-Command 'dotnet'

    & $dotnet --version | Set-Variable -Name "dotnetVersion"

    # this regex matches:
    #     5.0.408
    #         - suffix == ""
    #     9.0.100-preview.4.24267.66
    #         - suffix == "-preview.4.24267.66"
    #
    # not matches:
    #     5.0
    #     5.0.a
    #     blarblar
    if ($dotnetVersion -match '^\d+\.\d+\.\d+(?<suffix>(-|_|\w|\d|\.)*)?\s*$') {
        if ([System.String]::IsNullOrEmpty($Matches.suffix)) {
            # Stable version
            $content = @"
{
    "sdk": {
        "version": "$dotnetVersion",
        "allowPrerelease": false,
        "rollForward": "feature"
    }
}
"@
        }
        else {
            # Preview version
            $content = @"
{
    "sdk": {
        "version": "$dotnetVersion",
        "allowPrerelease": true,
        "rollForward": "major"
    }
}
"@
        }

        Out-File -FilePath $globalJsonPath -InputObject $content
        & Write-Host "Success to update global.json"
    }
    else {
        Write-Host "Failed to get dotnet version."
    }
}
finally {
    
}
