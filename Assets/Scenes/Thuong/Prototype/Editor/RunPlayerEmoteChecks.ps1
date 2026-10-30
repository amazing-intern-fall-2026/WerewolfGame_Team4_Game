param([ValidateRange(15, 120)][int]$TimeoutSeconds = 75)
$ErrorActionPreference = 'Stop'
$emoteWorkspace = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
$emoteQaDirectory = Join-Path $emoteWorkspace 'Logs\Emotes\Thuong'
$emoteExecutable = Join-Path $emoteQaDirectory 'Player\ThuongEmotes.exe'
if (!(Test-Path -LiteralPath $emoteExecutable)) { throw 'Run ThuongEmoteSystemChecks.RunAndBuildQa in Unity first.' }
$hostResult = Join-Path $emoteQaDirectory 'host-result.txt'
$clientResult = Join-Path $emoteQaDirectory 'client-result.txt'
$hostLog = Join-Path $emoteQaDirectory 'host.log'
$clientLog = Join-Path $emoteQaDirectory 'client.log'
Set-Content -LiteralPath $hostResult, $clientResult -Value 'PENDING'
$hostProcess = $null
$clientProcess = $null
try {
    $hostArguments = '-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --thuong-emote-qa host "' + $hostResult + '" -logFile "' + $hostLog + '"'
    $hostProcess = Start-Process -FilePath $emoteExecutable -ArgumentList $hostArguments -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    $clientArguments = '-batchmode -force-d3d11 -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --thuong-emote-qa client "' + $clientResult + '" -logFile "' + $clientLog + '"'
    $clientProcess = Start-Process -FilePath $emoteExecutable -ArgumentList $clientArguments -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((!$hostProcess.HasExited -or !$clientProcess.HasExited) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
    if (!$hostProcess.HasExited -or !$clientProcess.HasExited) { throw 'Emote QA timed out; inspect Logs/Emotes.' }
    $errors = @(Select-String -LiteralPath $hostLog, $clientLog -Pattern 'Exception:|Crash!!!' -ErrorAction SilentlyContinue)
    $passed = $hostProcess.ExitCode -eq 0 -and $clientProcess.ExitCode -eq 0 -and $errors.Count -eq 0 -and
        (Get-Content -LiteralPath $hostResult -Raw).StartsWith('PASS:') -and
        (Get-Content -LiteralPath $clientResult -Raw).StartsWith('PASS:')
    [ordered]@{ passed = $passed; hostExit = $hostProcess.ExitCode; clientExit = $clientProcess.ExitCode;
        errorCount = $errors.Count; testedAt = (Get-Date).ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $emoteQaDirectory 'network-result.json')
    Get-Content -LiteralPath $hostResult, $clientResult
    if (!$passed) { throw 'Emote QA failed; inspect host.log and client.log.' }
    Write-Output 'THUONG_EMOTES_REAL_PEERS_PASSED'
}
finally {
    foreach ($process in @($hostProcess, $clientProcess)) {
        if ($null -ne $process -and !$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    }
}
