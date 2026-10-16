param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
& (Join-Path $repo 'tests\run-curve-editor-tests.ps1') -BuildDirectory $BuildDirectory
& (Join-Path $repo 'tests\run-input-receiver-tests.ps1') -BuildDirectory $BuildDirectory
