# Unity 를 배치 모드로 한 번 실행하고 종료 코드를 그대로 돌려준다(Jenkins 단계에서 부른다, SPEC 8).
# 로그는 ci-logs/<Name>.log 에 남겨 Jenkins 가 보관한다.
#   ci/unity.ps1 -Name addressables -BuildTarget Android -Arguments '-executeMethod','AddressablesDelivery.Editor.CiBuild.Addressables','-deliveryMode','update'
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$BuildTarget,
    [string[]]$Arguments = @()
)

$ErrorActionPreference = 'Stop'

$unity = $env:UNITY_EXE
if ([string]::IsNullOrEmpty($unity)) {
    $unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe'
}
if (-not (Test-Path $unity)) {
    throw "Unity 실행 파일이 없다: $unity (UNITY_EXE 환경 변수로 지정)"
}

$project = (Get-Location).Path
$logDir = Join-Path $project 'ci-logs'
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir "$Name.log"

$all = @('-batchmode', '-nographics', '-projectPath', $project, '-buildTarget', $BuildTarget, '-logFile', $log) + $Arguments
Write-Host "Unity $($all -join ' ')"

# 공백이 들어간 인자는 따옴표로 감싸 한 덩어리로 넘긴다.
$quoted = $all | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
$process = Start-Process -FilePath $unity -ArgumentList $quoted -NoNewWindow -Wait -PassThru

Write-Host "Unity 종료 코드 $($process.ExitCode) (로그: $log)"
exit $process.ExitCode
