param([string]$Destination)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if (!$Destination) { $Destination=Join-Path $repo 'artifacts\upstream' }
$release=Invoke-RestMethod -Uri 'https://api.github.com/repos/RawAccelOfficial/rawaccel/releases/tags/v1.7.1' -Headers @{'User-Agent'='LightMove-build'}
$assets=@($release.assets | Where-Object { $_.name -match '(?i)rawaccel.*\.zip$' })
if ($assets.Count -ne 1) { throw 'Expected exactly one official Raw Accel v1.7.1 ZIP asset. Inspect the upstream release.' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$zip=Join-Path $Destination $assets[0].name
Invoke-WebRequest -Uri $assets[0].browser_download_url -OutFile $zip -UseBasicParsing
Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $Destination 'extracted') -Force
$drivers=@(Get-ChildItem -LiteralPath (Join-Path $Destination 'extracted') -Filter 'rawaccel.exe' -Recurse)
if ($drivers.Count -ne 1) { throw 'Official archive layout was unexpected.' }
$drivers[0].Directory.FullName
