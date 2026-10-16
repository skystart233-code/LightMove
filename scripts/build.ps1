param(
    [Parameter(Mandatory=$true)][string]$OfficialDirectory,
    [string]$OutputRoot,
    [switch]$SkipTests
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if (!$OutputRoot) { $OutputRoot=Join-Path $repo 'artifacts\LightMove' }
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
$build=Join-Path $OutputRoot 'app'
$metadata=Get-Content -LiteralPath (Join-Path $repo 'project.json') -Raw -Encoding UTF8 | ConvertFrom-Json
& (Join-Path $repo 'build-zh-CN.ps1') -OfficialDirectory $OfficialDirectory -OutputDirectory $build
if (!$SkipTests) { & (Join-Path $PSScriptRoot 'test.ps1') -BuildDirectory $build }
$package=Join-Path $OutputRoot ('LightMove-'+$metadata.version+'-windows-x64.exe')
& (Join-Path $repo 'packaging\build-lightmove.ps1') -AppBuildDirectory $build -OutputFile $package
Get-FileHash -LiteralPath $package -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath ($package+'.sha256') -Encoding ASCII
