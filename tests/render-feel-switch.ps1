param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$build=(Resolve-Path -LiteralPath $BuildDirectory).Path
$references=@(Get-Content -LiteralPath (Join-Path $repo 'artifacts\zh-CN-build\compile.rsp') | Where-Object { $_ -like '/reference:*' })
$arguments=$references+@('/nologo','/target:exe','/platform:x64','/langversion:7.3','/codepage:65001',
    ('/win32manifest:"'+(Join-Path $repo 'grapher\app.manifest')+'"'),
    ('/reference:"'+(Join-Path $build 'rawaccel.exe')+'"'),
    ('/out:"'+(Join-Path $build 'FeelSwitchPreview.exe')+'"'),
    ('"'+(Join-Path $PSScriptRoot 'FeelSwitchPreview.cs')+'"'))
$response=Join-Path $build 'feel-switch-preview.rsp'
$arguments | Set-Content -LiteralPath $response -Encoding UTF8
$sdk=@(& dotnet --list-sdks)[-1]
if($sdk -notmatch '^(\S+) \[(.+)\]') { throw '.NET SDK required.' }
$compiler=Join-Path $Matches[2] ($Matches[1]+'\Roslyn\bincore\csc.dll')
& dotnet $compiler /noconfig ('@'+$response)
if($LASTEXITCODE -ne 0) { throw 'Preview compilation failed.' }
$process=Start-Process -FilePath (Join-Path $build 'FeelSwitchPreview.exe') -WorkingDirectory $build -WindowStyle Hidden -PassThru
if(!$process.WaitForExit(10000) -or $process.ExitCode -ne 0) { throw 'Preview rendering failed.' }
Write-Output (Join-Path $build 'feel-switch-states.png')
