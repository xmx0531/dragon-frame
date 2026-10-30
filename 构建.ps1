param([string]$Output = "$PSScriptRoot\构建输出\程序")
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME="$PSScriptRoot\开发工具\cli-home"
$env:NUGET_PACKAGES="$PSScriptRoot\开发工具\nuget"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$dotnetExe=Join-Path $PSScriptRoot '开发工具\dotnet\dotnet.exe'
if(!(Test-Path -LiteralPath $dotnetExe)){
    $dotnetCommand=Get-Command dotnet -ErrorAction SilentlyContinue
    if($null -eq $dotnetCommand){throw '缺少 .NET 8 SDK。请先从微软官网下载并安装。'}
    $dotnetExe=$dotnetCommand.Source
}
& $dotnetExe publish "$PSScriptRoot\源码\DragonFrame\DragonFrame.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -o $Output
if($LASTEXITCODE -ne 0){throw '构建失败'}
Move-Item -LiteralPath "$Output\DragonFrame.exe" -Destination "$Output\小龙娘相框.exe" -Force
Copy-Item -LiteralPath "$PSScriptRoot\licenses" -Destination $Output -Recurse -Force


