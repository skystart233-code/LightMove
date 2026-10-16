param([string]$BuildDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\RawAccel-zh-CN'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$build = (Resolve-Path -LiteralPath $BuildDirectory).Path
$compiledArguments = Join-Path $repo 'artifacts\zh-CN-build\compile.rsp'
$references = @(Get-Content -LiteralPath $compiledArguments | Where-Object { $_ -like '/reference:*' })
$arguments = $references + @('/nologo', '/target:exe', '/platform:x64', '/langversion:7.3', '/codepage:65001',
    ('/out:"' + (Join-Path $build 'CurveEditorTests.exe') + '"'),
    ('/reference:"' + (Join-Path $build 'rawaccel.exe') + '"'),
    ('"' + (Join-Path $PSScriptRoot 'CurveEditorTests.cs') + '"'))
$response = Join-Path $build 'test-compile.rsp'
$arguments | Set-Content -LiteralPath $response -Encoding UTF8
$sdk = @(& dotnet --list-sdks)[-1]
if ($sdk -notmatch '^(\S+) \[(.+)\]') { throw '.NET SDK required.' }
$compiler = Join-Path $Matches[2] ($Matches[1] + '\Roslyn\bincore\csc.dll')
& dotnet $compiler /noconfig ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$process = Start-Process -FilePath (Join-Path $build 'CurveEditorTests.exe') -WorkingDirectory $build -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(30000)) { Stop-Process -Id $process.Id; throw 'Tests timed out.' }
$result = Get-Content -LiteralPath (Join-Path $build 'curve-editor-test-result.txt') -Raw
Write-Output $result
if ($process.ExitCode -ne 0 -or !$result.StartsWith('PASS:')) { throw 'Curve editor tests failed.' }
