param(
    [ValidateSet('build', 'manual', 'bridge', 'train', 'forage')][string]$Mode = 'manual',
    [string]$Editor = '',
    [string]$Python = '',
    [string]$OutputRoot = '',
    [int]$Seed = 1,
    [int]$Steps = 512,
    [ValidateRange(1, 2147483647)][int]$TrainSteps = 2048,
    [string]$ResumeFrom = '',
    [string]$WorldConfig = '',
    [string]$ReloadRun = '',
    [string]$Scenario = '',
    [switch]$Visible,
    [ValidateSet('rules', 'idle', 'manual', 'scripted')][string]$Controller = 'rules'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Editor) { $Editor = Join-Path $root '.tools/unity/6000.0.85f1/Editor/Unity.exe' }
if (-not $Python) { $Python = Join-Path $root '.venv/Scripts/python.exe' }
if (-not $OutputRoot) { $OutputRoot = Join-Path $root 'artifacts/runs' }
$OutputRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputRoot)
$project = Join-Path $root 'unity'
$player = Join-Path $project 'Builds/World.exe'
$config = Join-Path $root 'training/world.json'
if ($Mode -eq 'forage') { $config = Join-Path $root 'training/foraging.json' }
if ($WorldConfig) { $config = (Resolve-Path -LiteralPath $WorldConfig).Path }
if ($ResumeFrom -and $Mode -ne 'train') { throw 'ResumeFrom 僅適用於 train。' }
if ($Scenario -and $Mode -ne 'forage') { throw 'Scenario 僅適用於 forage。' }
if ($Scenario) { $Scenario = (Resolve-Path -LiteralPath $Scenario).Path; $Controller = 'scripted' }
$runId = '{0}-{1}-{2}' -f $Mode, (Get-Date -Format 'yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$output = Join-Path $OutputRoot $runId
New-Item -ItemType Directory -Force $OutputRoot | Out-Null
if ($ReloadRun -and $Mode -ne 'forage') { throw 'ReloadRun 僅適用於 forage。' }
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
    $utf8 = [Text.UTF8Encoding]::new($false)
    $trainerYaml = (Get-Content -Raw (Join-Path $root 'training/toolchain.yaml')) -replace 'max_steps: \d+', "max_steps: $TrainSteps"
    [IO.File]::WriteAllText($trainerConfig, $trainerYaml, $utf8)
    $timeScale = if ($Visible) { '1' } else { '20' }
    $arguments = @('-m', 'mlagents.trainers.learn', $trainerConfig, '--env', $player, '--run-id', $trainerRunId, '--results-dir', $output, '--seed', $Seed, '--torch-device', 'cpu', '--time-scale', $timeScale, '--width', '960', '--height', '720', '--max-lifetime-restarts', '0')
    if ($ResumeFrom) { $arguments += '--resume' }
    if (-not $Visible) { $arguments += '--no-graphics' }
    $arguments += @('--env-args', '--world-log', (Join-Path $output 'world.jsonl'), '--world-seed', $Seed, '--world-config', $config, '--control', 'python')
    $trainingRecord = @{ source = $ResumeFrom; trainerRunId = $trainerRunId; targetSteps = $TrainSteps; worldConfig = (Get-Content -Raw $config | ConvertFrom-Json) } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $output 'training.json'), $trainingRecord, $utf8)
    $trainerLog = Join-Path $output 'trainer.log'
    [IO.File]::WriteAllText($trainerLog, '', $utf8)
    # PowerShell 5.1 合併 stderr 時會把套件警告當成終止錯誤；訓練日誌由 stdout 保存。
    & $Python @arguments | ForEach-Object {
        [IO.File]::AppendAllText($trainerLog, $_.ToString() + [Environment]::NewLine, $utf8)
        Write-Output $_
    }
    if ($LASTEXITCODE -ne 0) { throw '訓練失敗。' }
} elseif ($Mode -eq 'forage') {
    New-Item -ItemType Directory $output | Out-Null
    if ($ReloadRun) {
        if ($WorldConfig -or $Scenario -or $PSBoundParameters.ContainsKey('Seed') -or $PSBoundParameters.ContainsKey('Controller')) { throw 'ReloadRun 不可同時指定 WorldConfig、Scenario、Seed 或 Controller。' }
        $record = Get-Content -LiteralPath $ReloadRun -TotalCount 1 | ConvertFrom-Json
        if ($record.kind -ne 'run' -or $record.rules -ne 'skills-v1' -or -not $record.config.foraging) { throw '不是相容的執行紀錄（須為 skills-v1）。' }
        $Seed = [int]$record.seed
        $Controller = $record.control
        $config = Join-Path $output 'world-config.json'
        [IO.File]::WriteAllText($config, ($record.config | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
        if ($Controller -eq 'scripted') {
            $Scenario = Join-Path $output 'scenario.json'
            [IO.File]::WriteAllText($Scenario, ($record.scenario | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
        }
    }
    if (-not $PSBoundParameters.ContainsKey('Steps')) { $Steps = if ($Visible) { 0 } else { 12000 } }
    $arguments = @('--control', $Controller, '--world-seed', $Seed,  '--world-config', ('"{0}"' -f $config), '--world-log', ('"{0}"' -f (Join-Path $output 'world.jsonl')), '--world-steps' , $Steps)
    if ($Controller -eq 'scripted') {
        if (-not $Scenario) { throw 'scripted 控制須提供 Scenario。' }
        $arguments += @('--world-scenario', ('"{0}"' -f $Scenario))
    }
    if (-not $Visible) {
        if ($Steps -le 0) { throw '無畫面執行須指定正數 Steps。' }
        $arguments += @('-batchmode', '-nographics')
    }
    $launch = @{ FilePath = $player; ArgumentList = $arguments; Wait = $true; PassThru = $true }
    if (-not $Visible) { $launch.WindowStyle = 'Hidden' }
    $process = Start-Process @launch
    if ($process.ExitCode -ne 0) { throw '覓食世界執行失敗。' }
} else {
    New-Item -ItemType Directory $output | Out-Null
    & $player --control manual --world-seed $Seed --world-config $config --world-log (Join-Path $output 'world.jsonl')
}
Write-Output $output
