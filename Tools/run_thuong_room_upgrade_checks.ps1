param(
    [ValidateSet('Unit','Online','Build','All')][string]$Mode = 'Online',
    [string]$UnityPath = '',
    [ValidateRange(60,1800)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$upgradeWorkspace = Split-Path -Parent $PSScriptRoot
$upgradeLogs = Join-Path $upgradeWorkspace 'Logs\RoomUpgrades'
New-Item -ItemType Directory -Force -Path $upgradeLogs | Out-Null
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $upgradeVersion = ((Get-Content -LiteralPath (Join-Path $upgradeWorkspace 'ProjectSettings\ProjectVersion.txt') -TotalCount 1) -split ': ')[1]
    $upgradeCandidates = @(
        (Join-Path ${env:ProgramFiles} "Unity Hub\Editor\$upgradeVersion\Editor\Unity.exe"),
        "D:\Software\Unity Editor\$upgradeVersion\Editor\Unity.exe"
    )
    $UnityPath = $upgradeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$UnityPath -or !(Test-Path -LiteralPath $UnityPath)) { throw 'Pass -UnityPath pointing to the Unity Editor executable.' }
$upgradeRuns = switch ($Mode) {
    'Unit' { @(@{ Name='unit'; Method='ThuongRoomUpgradeChecks.Run'; Graphics=$false }) }
    'Online' { @(@{ Name='online'; Method='ThuongRoomUpgradeOnlineChecks.Run'; Graphics=$true }) }
    'Build' { @(@{ Name='build'; Method='ThuongRoomUpgradeChecks.VerifyAndBuild'; Graphics=$false }) }
    'All' { @(
        @{ Name='build'; Method='ThuongRoomUpgradeChecks.VerifyAndBuild'; Graphics=$false },
        @{ Name='online'; Method='ThuongRoomUpgradeOnlineChecks.Run'; Graphics=$true }
    ) }
}
foreach ($upgradeRun in $upgradeRuns) {
    $upgradeLog = Join-Path $upgradeLogs ($upgradeRun.Name + '.log')
    $upgradeGraphics = if ($upgradeRun.Graphics) { '-force-d3d11 -screen-width 1280 -screen-height 720' } else { '-nographics -quit' }
    $upgradeArguments = '-batchmode ' + $upgradeGraphics + ' -projectPath "' + $upgradeWorkspace + '" -executeMethod ' + $upgradeRun.Method + ' -logFile "' + $upgradeLog + '"'
    $upgradeProcess = Start-Process -FilePath $UnityPath -ArgumentList $upgradeArguments -WindowStyle Hidden -PassThru
    try {
        if (!$upgradeProcess.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-Process -Id $upgradeProcess.Id
            throw "Unity $($upgradeRun.Name) checks timed out; inspect $upgradeLog"
        }
        $upgradeProcess.Refresh()
        Select-String -LiteralPath $upgradeLog -Pattern 'THUONG_.*PASSED|THUONG_ROOM_UPGRADE_ONLINE_FAILED|error CS' |
            ForEach-Object { Write-Output $_.Line }
        if ($upgradeProcess.ExitCode -ne 0) { throw "Unity $($upgradeRun.Name) exit=$($upgradeProcess.ExitCode); inspect $upgradeLog" }
    }
    finally {
        if (!$upgradeProcess.HasExited) { Stop-Process -Id $upgradeProcess.Id }
    }
}
