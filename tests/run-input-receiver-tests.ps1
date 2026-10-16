param([Parameter(Mandatory=$true)][string]$BuildDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$build=(Resolve-Path -LiteralPath $BuildDirectory).Path
$qa=Join-Path $repo ('artifacts\input-tests\'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qa -Force | Out-Null
foreach($name in @('wrapper.dll','Newtonsoft.Json.dll')) { Copy-Item -LiteralPath (Join-Path $build $name) -Destination $qa }
$rsp=Get-Content -LiteralPath (Join-Path $repo 'artifacts\zh-CN-build\compile.rsp') -Raw
$form=Join-Path $repo 'grapher\Form1.cs'
$manager=Join-Path $repo 'grapher\Models\Serialized\SettingsManager.cs'
$designer=Join-Path $repo 'grapher\Form1.Designer.cs'
$formCopy=Join-Path $qa 'Form1.cs'
$managerCopy=Join-Path $qa 'SettingsManager.cs'
$designerCopy=Join-Path $qa 'Form1.Designer.cs'
(Get-Content -LiteralPath $form -Raw).Replace('VersionHelper.ValidOrThrow()', 'new Version(1,7,1)') | Set-Content -LiteralPath $formCopy -Encoding UTF8
(Get-Content -LiteralPath $manager -Raw).Replace('DriverConfig.GetActive()', 'DriverConfig.GetDefault()').Replace('ActiveConfig.Activate()', 'System.Diagnostics.Debug.Fail("Driver activation prohibited during QA")').Replace('DriverConfig.Deactivate()', 'System.Diagnostics.Debug.Fail("Driver reset prohibited during QA")') | Set-Content -LiteralPath $managerCopy -Encoding UTF8
[regex]::Replace((Get-Content -LiteralPath $designer -Raw), '(?m)^.*this\.(FormClosing|Shown) \+=.*\r?\n', '') | Set-Content -LiteralPath $designerCopy -Encoding UTF8
$rsp=$rsp.Replace($form,$formCopy).Replace($manager,$managerCopy).Replace($designer,$designerCopy)
$rsp=[regex]::Replace($rsp,'/out:"[^"]+"',('/out:"'+(Join-Path $qa 'InputReceiverTests.exe')+'"'))
$rsp += "`r`n/main:InputReceiverTests`r`n" + '"' + (Join-Path $PSScriptRoot 'InputReceiverTests.cs') + '"'
$response=Join-Path $qa 'compile.rsp'
$rsp | Set-Content -LiteralPath $response -Encoding UTF8
$sdk=@(& dotnet --list-sdks)[-1]
if($sdk -notmatch '^(\S+) \[(.+)\]') { throw '.NET SDK required.' }
& dotnet (Join-Path $Matches[2] ($Matches[1]+'\Roslyn\bincore\csc.dll')) /noconfig ('@'+$response)
if($LASTEXITCODE -ne 0) { throw 'Input test build failed.' }
$p=Start-Process -FilePath (Join-Path $qa 'InputReceiverTests.exe') -WorkingDirectory $qa -WindowStyle Hidden -PassThru
if(!$p.WaitForExit(30000)) { throw 'Input tests timed out.' }
$result=Get-Content -LiteralPath (Join-Path $qa 'input-receiver-test-result.txt') -Raw
$result
if($p.ExitCode -ne 0 -or !$result.StartsWith('PASS:')) { throw 'Input tests failed.' }
