param([string]$Destination)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if (!$Destination) { $Destination=Join-Path $repo 'artifacts\upstream' }
# Fixed upstream asset avoids the shared anonymous GitHub API quota on CI.
$uri='https://github.com/RawAccelOfficial/rawaccel/releases/download/v1.7.1/RawAccel_v1.7.1.zip'
$expectedHash='770FE3AE0919CA3C4D412F58C985EB27F5434DECAD809F7E8206DE4E8852EEC4'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$zip=Join-Path $Destination 'RawAccel_v1.7.1.zip'
Invoke-WebRequest -Uri $uri -OutFile $zip -UseBasicParsing
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Official dependency ZIP checksum mismatch.' }
Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $Destination 'extracted') -Force
$drivers=@(Get-ChildItem -LiteralPath (Join-Path $Destination 'extracted') -Filter 'rawaccel.exe' -Recurse)
if ($drivers.Count -ne 1) { throw 'Official archive layout was unexpected.' }
$drivers[0].Directory.FullName
