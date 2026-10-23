param(
    [Parameter(Mandatory=$true)][string]$AppBuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputFile
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$app=(Resolve-Path -LiteralPath $AppBuildDirectory).Path
$output=[IO.Path]::GetFullPath($OutputFile)
New-Item -ItemType Directory -Path (Split-Path $output -Parent) -Force | Out-Null
$stage=Join-Path $repo ('artifacts\lightmove-package\'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $stage 'driver') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $app 'rawaccel.exe') -Destination (Join-Path $stage 'LightMove.exe')
Copy-Item -LiteralPath (Join-Path $app 'LightMove.CursorGuard.exe') -Destination (Join-Path $stage 'LightMove.CursorGuard.exe')
Copy-Item -LiteralPath (Join-Path $app 'rawaccel.exe.config') -Destination (Join-Path $stage 'LightMove.exe.config')
foreach($name in @('wrapper.dll','Newtonsoft.Json.dll','writer.exe','installer.exe','uninstaller.exe','LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $app $name) -Destination (Join-Path $stage $name)
}
Copy-Item -LiteralPath (Join-Path $app 'driver\rawaccel.sys') -Destination (Join-Path $stage 'driver\rawaccel.sys')
Copy-Item -LiteralPath (Join-Path $repo 'README.zh-CN.md') -Destination (Join-Path $stage '使用说明.md')
$thirdPartyDir=Join-Path $stage 'third-party'
$shakeDir=Join-Path $thirdPartyDir 'ShakeToFindCursor'
$newtonDir=Join-Path $thirdPartyDir 'Newtonsoft.Json'
New-Item -ItemType Directory -Path $shakeDir,$newtonDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $stage 'LICENSE')
Copy-Item -LiteralPath (Join-Path $repo 'third-party\ShakeToFindCursor\LICENSE') -Destination (Join-Path $shakeDir 'LICENSE')
Copy-Item -LiteralPath (Join-Path $repo 'third-party\ShakeToFindCursor\NOTICE.md') -Destination (Join-Path $shakeDir 'NOTICE.md')
Copy-Item -LiteralPath (Join-Path $repo 'third-party\Newtonsoft.Json\LICENSE.md') -Destination (Join-Path $newtonDir 'LICENSE.md')
Copy-Item -LiteralPath (Join-Path $repo 'third-party\Newtonsoft.Json\NOTICE.md') -Destination (Join-Path $newtonDir 'NOTICE.md')
Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $stage 'THIRD_PARTY_NOTICES.md')
$thirdParty=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md')
$shakeNotice=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repo 'third-party\ShakeToFindCursor\NOTICE.md')
$rawLicense=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repo 'LICENSE')
$shakeLicense=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repo 'third-party\ShakeToFindCursor\LICENSE')
$newtonLicense=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $repo 'third-party\Newtonsoft.Json\LICENSE.md')
$sections=@(
    $thirdParty,
    ("# ShakeToFindCursor 来源与改动`r`n`r`n"+$shakeNotice),
    ("# Raw Accel / LightMove MIT License`r`n`r`n"+$rawLicense),
    ("# ShakeToFindCursor MIT License`r`n`r`n"+$shakeLicense),
    ("# Newtonsoft.Json 13.0.1 MIT License`r`n`r`n"+$newtonLicense)
)
($sections -join "`r`n`r`n---`r`n`r`n") | Set-Content -LiteralPath (Join-Path $stage '开源组件许可.txt') -Encoding UTF8
@'
轻移 LightMove · 0.5.0 社区预览版
独立维护仓库：https://github.com/skystart233-code/LightMove
基于 RawAccelOfficial/rawaccel v1.7.1，使用官方原版驱动和封装库。
上游：https://github.com/RawAccelOfficial/rawaccel
社区扩展界面，非 Raw Accel 官方发行版；LightMove 不代表全部原创。
许可证：MIT，详见随附 LICENSE 与 THIRD_PARTY_NOTICES.md；开源组件许可.txt 汇总三份完整许可。
首次启动会在当前用户的 LocalAppData\Programs\LightMove 解包所需文件。
驱动已就绪时直接进入主界面；否则显示安装及重启引导。
适用于 Windows 10/11 x64，需要 .NET Framework 4.7.2+ 和 Visual C++ x64 运行库。
'@ | Set-Content -LiteralPath (Join-Path $stage '版本与来源.txt') -Encoding UTF8
$payload=Join-Path (Split-Path $stage -Parent) ([IO.Path]::GetFileName($stage)+'.zip')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $payload
$refs=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$arguments=@('/nologo','/nostdlib+','/target:winexe','/platform:x64','/langversion:7.3','/codepage:65001',('/out:"'+$output+'"'),
    ('/win32manifest:"'+(Join-Path $repo 'grapher\app.manifest')+'"'),('/win32icon:"'+(Join-Path $repo 'grapher\lightmove.ico')+'"'),
    ('/resource:"'+$payload+'",LightMove.Payload.zip'))
foreach($name in @('mscorlib','System','System.Core','System.Windows.Forms','System.Drawing','System.IO.Compression','System.IO.Compression.FileSystem')) {
    $arguments += '/reference:"'+(Join-Path $refs ($name+'.dll'))+'"'
}
$arguments += '"'+(Join-Path $PSScriptRoot 'LightMoveLauncher.cs')+'"'
$arguments += '"'+(Join-Path $repo 'grapher\CurveEditing\LightTheme.cs')+'"'
$response=Join-Path $stage 'launcher.rsp'
$arguments | Set-Content -LiteralPath $response -Encoding UTF8
$sdk=@(& dotnet --list-sdks)[-1]
if($sdk -notmatch '^(\S+) \[(.+)\]') { throw '.NET SDK required.' }
$compiler=Join-Path $Matches[2] ($Matches[1]+'\Roslyn\bincore\csc.dll')
& dotnet $compiler /noconfig ('@'+$response)
if($LASTEXITCODE -ne 0) { throw 'Launcher compilation failed.' }
Get-Item -LiteralPath $output | Select-Object FullName,Length


