param(
    [Parameter(Mandatory = $true)][string]$OfficialDirectory,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\RawAccel-zh-CN')
)
$ErrorActionPreference = 'Stop'
$OfficialDirectory = (Resolve-Path -LiteralPath $OfficialDirectory).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if ($outputPath -eq $OfficialDirectory) { throw 'Output must differ from official input.' }
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Copy-Item -Path (Join-Path $OfficialDirectory '*') -Destination $outputPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $OfficialDirectory 'rawaccel.exe') -Destination (Join-Path $outputPath 'rawaccel-original.exe') -Force

$buildPath = Join-Path $PSScriptRoot 'artifacts\zh-CN-build'
New-Item -ItemType Directory -Path $buildPath -Force | Out-Null
# The official application targets .NET Framework; generate its WinForms resources
# with Windows PowerShell's Framework runtime and compile with the .NET SDK's Roslyn.
$resourceScript = Join-Path $buildPath 'resources.ps1'
@'
param([string]$Source, [string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$items = @(
    @('AboutBox.resx', 'grapher.AboutBox.resources'),
    @('Form1.resx', 'grapher.RawAcceleration.resources'),
    @('Properties\Resources.resx', 'grapher.Properties.Resources.resources')
)
foreach ($item in $items) {
    $reader = New-Object System.Resources.ResXResourceReader (Join-Path $Source $item[0])
    $reader.BasePath = Split-Path (Join-Path $Source $item[0])
    $writer = New-Object System.Resources.ResourceWriter (Join-Path $Output $item[1])
    try {
        foreach ($entry in $reader) { $writer.AddResource($entry.Key, $entry.Value) }
        $writer.Generate()
    } finally { $writer.Close(); $reader.Close() }
}
'@ | Set-Content -LiteralPath $resourceScript -Encoding UTF8
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $resourceScript -Source (Join-Path $PSScriptRoot 'grapher') -Output $buildPath
if ($LASTEXITCODE -ne 0) { throw 'Resource generation failed.' }

$sdkLine = @(& dotnet --list-sdks)[-1]
if ($sdkLine -notmatch '^(\S+) \[(.+)\]') { throw '.NET SDK is required.' }
$compiler = Join-Path $Matches[2] ($Matches[1] + '\Roslyn\bincore\csc.dll')
$refs = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
if (!(Test-Path -LiteralPath $refs)) {
    $refs = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319'
}
$arguments = @('/nologo', '/nostdlib+', '/target:winexe', '/platform:x64',
    '/langversion:7.3', '/codepage:65001', '/optimize+', '/deterministic+',
    ('/out:"' + (Join-Path $outputPath 'rawaccel.exe') + '"'),
    ('/win32icon:"' + (Join-Path $PSScriptRoot 'grapher\lightmove.ico') + '"'))
$arguments += '/win32manifest:"' + (Join-Path $PSScriptRoot 'grapher\app.manifest') + '"'
foreach ($name in @('mscorlib','System','System.Core','System.Windows.Forms','System.Windows.Forms.DataVisualization',
    'System.Drawing','System.Xml','System.Xml.Linq','System.Data','System.Data.DataSetExtensions',
    'Microsoft.CSharp','System.Deployment','System.Net.Http','System.Configuration')) {
    $arguments += '/reference:"' + (Join-Path $refs ($name + '.dll')) + '"'
}
foreach ($name in @('wrapper.dll', 'Newtonsoft.Json.dll')) {
    $arguments += '/reference:"' + (Join-Path $OfficialDirectory $name) + '"'
}
foreach ($item in Get-ChildItem -LiteralPath $buildPath -Filter '*.resources') {
    $arguments += '/resource:"' + $item.FullName + '",' + $item.Name
}
foreach ($item in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'grapher') -Filter '*.cs' -Recurse) {
    if ($item.FullName -notmatch '\\(obj|bin)\\') { $arguments += '"' + $item.FullName + '"' }
}
$responseFile = Join-Path $buildPath 'compile.rsp'
$arguments | Set-Content -LiteralPath $responseFile -Encoding UTF8
& dotnet $compiler /noconfig ('@' + $responseFile)
if ($LASTEXITCODE -ne 0) { throw 'Chinese GUI compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'grapher\App.config') -Destination (Join-Path $outputPath 'rawaccel.exe.config') -Force
Write-Output "Chinese GUI built: $outputPath"
