param(
    [ValidateSet('d3d11','none')][string]$Graphics = 'd3d11',
    [ValidateRange(30,600)][int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$qaDirectory = Join-Path $workspaceRoot 'Logs\Maps'
$qaExecutable = Join-Path $qaDirectory 'QaPlayer\ThuongMaps.exe'
if (!(Test-Path -LiteralPath $qaExecutable)) { throw 'Build Tools/Thuong/Maps/Build Independent Multiplayer QA first.' }
$hostResult = Join-Path $qaDirectory 'independent-host.txt'
$clientResult = Join-Path $qaDirectory 'independent-client.txt'
$hostLog = Join-Path $qaDirectory 'independent-host.log'
$clientLog = Join-Path $qaDirectory 'independent-client.log'
Set-Content -LiteralPath $hostResult,$clientResult -Value 'RUN_PENDING'
[ordered]@{ passed=$false; status='RUN_PENDING' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qaDirectory 'independent-process-result.json')
$graphicsArguments = if ($Graphics -eq 'none') { '-nographics' } else { '-force-d3d11' }
$baseArguments = '-batchmode ' + $graphicsArguments + ' -screen-width 1280 -screen-height 720 -screen-fullscreen 0'
$hostProcess = $null; $clientProcess = $null
try {
    $hostArguments = $baseArguments + ' --thuong-map-qa host "' + $hostResult + '" -logFile "' + $hostLog + '"'
    $hostProcess = Start-Process -FilePath $qaExecutable -ArgumentList $hostArguments -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $clientArguments = $baseArguments + ' --thuong-map-qa client "' + $clientResult + '" -logFile "' + $clientLog + '"'
    $clientProcess = Start-Process -FilePath $qaExecutable -ArgumentList $clientArguments -WindowStyle Hidden -PassThru
    Write-Output "QA host PID=$($hostProcess.Id), client PID=$($clientProcess.Id)"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((!$hostProcess.HasExited -or !$clientProcess.HasExited) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
    foreach ($process in @($hostProcess,$clientProcess)) {
        if (!$process.HasExited) { Stop-Process -Id $process.Id; throw "QA process timed out: $($process.Id)" }
    }
    $errors = @(Select-String -LiteralPath $hostLog,$clientLog -Pattern 'Exception:|Crash!!!|could not be loaded from the resource file' -ErrorAction SilentlyContinue)
    $passed = $hostProcess.ExitCode -eq 0 -and $clientProcess.ExitCode -eq 0 -and $errors.Count -eq 0 -and
        (Get-Content -LiteralPath $hostResult -Raw).Contains('_PASSED:') -and
        (Get-Content -LiteralPath $clientResult -Raw).Contains('_PASSED:')
    [ordered]@{ passed=$passed; hostExit=$hostProcess.ExitCode; clientExit=$clientProcess.ExitCode;
        graphics=$Graphics; errorCount=$errors.Count; testedAt=(Get-Date).ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qaDirectory 'independent-process-result.json')
    Get-Content -LiteralPath $hostResult,$clientResult
    if ($errors.Count -gt 0) { $errors | Select-Object -First 6 | ForEach-Object { Write-Output $_.Line } }
    if (!$passed) { throw 'Independent QA failed: inspect exit codes and player logs.' }
    Write-Output 'THUONG_MAP_INDEPENDENT_PROCESSES_PASSED: both exit codes 0; no C# exception, crash or missing resource in player logs.'
    Get-Content -LiteralPath ($hostResult + '.performance.txt') -ErrorAction SilentlyContinue
}
finally {
    foreach ($process in @($hostProcess,$clientProcess)) {
        if ($null -ne $process -and !$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    }
}
