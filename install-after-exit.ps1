param(
    [Parameter(Mandatory = $true)]
    [int] $ProcessId,

    [Parameter(Mandatory = $true)]
    [string] $SourcePath,

    [Parameter(Mandatory = $true)]
    [string] $DestinationPath,

    [Parameter(Mandatory = $true)]
    [string] $LogPath
)

$ErrorActionPreference = 'Stop'

try {
    Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue

    $installed = $false
    for ($attempt = 1; $attempt -le 40; $attempt++) {
        try {
            Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
            $installed = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    if (!$installed) {
        throw 'The DLL remained locked after the game process exited.'
    }

    $sourceHash = (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash
    $destinationHash = (Get-FileHash -LiteralPath $DestinationPath -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) {
        throw 'The installed DLL hash does not match the build.'
    }

    "Installed DoktorMod.dll after process $ProcessId exited. SHA256=$destinationHash" |
        Set-Content -LiteralPath $LogPath
}
catch {
    "Install failed after process $ProcessId exited: $($_.Exception.Message)" |
        Set-Content -LiteralPath $LogPath
    exit 1
}
