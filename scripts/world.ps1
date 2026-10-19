param(
    [ValidateSet('build', 'manual', 'bridge', 'train')][string]$Mode = 'manual',
    [string]$Editor = 'C:/Toolchains/Unity/6000.0.85f1/Editor/Unity.exe',
    [string]$Python = 'C:/Toolchains/game-ecosystem/python/Scripts/python.exe',
    [string]$OutputRoot = 'C:/Toolchains/game-ecosystem/runs',
    [int]$Seed = 1,
    [int]$Steps = 512,
    [switch]$Visible
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'unity'
$player = Join-Path $project 'Builds/World.exe'
$config = Join-Path $root 'training/world.json'
$runId = '{0}-{1}-{2}' -f $Mode, (Get-Date -Format 'yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$output = Join-Path $OutputRoot $runId
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
if ($Mode -eq 'build') {
    New-Item -ItemType Directory $output | Out-Null
    $log = Join-Path $output 'build.log'
    $process = Start-Process -FilePath $Editor -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"{0}"' -f $project), '-executeMethod', 'Ecosystem.Editor.BuildWorld.Build', '-logFile', ('"{0}"' -f $log)) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Unity 建置失敗，請查看 $log" }
    Write-Output $log
    exit 0
}
if (-not (Test-Path $player)) { throw '請先執行 -Mode build。' }
if ($Mode -eq 'bridge') {
    $arguments = @((Join-Path $root 'training/bridge.py'), '--env', $player, '--config', $config, '--output', $output, '--seed', $Seed, '--steps', $Steps)
    if ($Visible) { $arguments += '--visible' }
    & $Python @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Python 往返驗證失敗。' }
} elseif ($Mode -eq 'train') {
    New-Item -ItemType Directory $output | Out-Null
    $timeScale = if ($Visible) { '1' } else { '20' }
    $arguments = @('-m', 'mlagents.trainers.learn', (Join-Path $root 'training/toolchain.yaml'), '--env', $player, '--run-id', $runId, '--results-dir', $output, '--seed', $Seed, '--torch-device', 'cpu', '--time-scale', $timeScale, '--width', '960', '--height', '720', '--max-lifetime-restarts', '0')
    if (-not $Visible) { $arguments += '--no-graphics' }
    $arguments += @('--env-args', '--world-log', (Join-Path $output 'world.jsonl'), '--world-seed', $Seed, '--world-config', $config, '--control', 'python')
    & $Python @arguments
    if ($LASTEXITCODE -ne 0) { throw '訓練失敗。' }
} else {
    New-Item -ItemType Directory $output | Out-Null
    & $player --control manual --world-seed $Seed --world-config $config --world-log (Join-Path $output 'world.jsonl')
}
Write-Output $output
