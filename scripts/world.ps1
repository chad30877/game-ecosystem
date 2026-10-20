param(
    [ValidateSet('build', 'manual', 'bridge', 'train')][string]$Mode = 'manual',
    [string]$Editor = 'C:/Toolchains/Unity/6000.0.85f1/Editor/Unity.exe',
    [string]$Python = 'C:/Toolchains/game-ecosystem/python/Scripts/python.exe',
    [string]$OutputRoot = 'C:/Toolchains/game-ecosystem/runs',
    [int]$Seed = 1,
    [int]$Steps = 512,
    [ValidateRange(1, 2147483647)][int]$TrainSteps = 2048,
    [string]$ResumeFrom = '',
    [string]$WorldConfig = '',
    [switch]$Visible
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'unity'
$player = Join-Path $project 'Builds/World.exe'
$config = Join-Path $root 'training/world.json'
if ($WorldConfig) { $config = (Resolve-Path -LiteralPath $WorldConfig).Path }
if ($ResumeFrom -and $Mode -ne 'train') { throw 'ResumeFrom 僅適用於 train。' }
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
    $trainerRunId = $runId
    if ($ResumeFrom) {
        $source = (Resolve-Path -LiteralPath $ResumeFrom).Path
        foreach ($species in @('Flame', 'Frost', 'Moss')) {
            if (-not (Test-Path -LiteralPath (Join-Path $source "$species/checkpoint.pt"))) {
                throw "缺少 $species/checkpoint.pt；ResumeFrom 應指向含三物種 checkpoint 的訓練目錄。"
            }
        }
        $resumeCheck = @'
import sys
from pathlib import Path
import torch
for species in ('Flame', 'Frost', 'Moss'):
    checkpoint = torch.load(Path(sys.argv[1]) / species / 'checkpoint.pt', weights_only=True)
    step = int(checkpoint['global_step']['_GlobalSteps__global_step'][0])
    if int(sys.argv[2]) <= step:
        raise ValueError(f'TrainSteps must exceed {species} checkpoint step {step}')
'@
        & $Python -c $resumeCheck $source $TrainSteps
        if ($LASTEXITCODE -ne 0) { throw '續訓檢查失敗；TrainSteps 必須大於三物種已保存步數。' }
        $trainerRunId = Split-Path $source -Leaf
        Copy-Item -LiteralPath $source -Destination (Join-Path $output $trainerRunId) -Recurse
    }
    $trainerConfig = Join-Path $output 'trainer.yaml'
    (Get-Content -Raw (Join-Path $root 'training/toolchain.yaml')) -replace 'max_steps: \d+', "max_steps: $TrainSteps" | Set-Content -Encoding utf8 $trainerConfig
    $timeScale = if ($Visible) { '1' } else { '20' }
    $arguments = @('-m', 'mlagents.trainers.learn', $trainerConfig, '--env', $player, '--run-id', $trainerRunId, '--results-dir', $output, '--seed', $Seed, '--torch-device', 'cpu', '--time-scale', $timeScale, '--width', '960', '--height', '720', '--max-lifetime-restarts', '0')
    if ($ResumeFrom) { $arguments += '--resume' }
    if (-not $Visible) { $arguments += '--no-graphics' }
    $arguments += @('--env-args', '--world-log', (Join-Path $output 'world.jsonl'), '--world-seed', $Seed, '--world-config', $config, '--control', 'python')
    @{ source = $ResumeFrom; trainerRunId = $trainerRunId; targetSteps = $TrainSteps; worldConfig = (Get-Content -Raw $config | ConvertFrom-Json) } | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $output 'training.json')
    & $Python @arguments 2>&1 | Tee-Object -FilePath (Join-Path $output 'trainer.log')
    if ($LASTEXITCODE -ne 0) { throw '訓練失敗。' }
} else {
    New-Item -ItemType Directory $output | Out-Null
    & $player --control manual --world-seed $Seed --world-config $config --world-log (Join-Path $output 'world.jsonl')
}
Write-Output $output
