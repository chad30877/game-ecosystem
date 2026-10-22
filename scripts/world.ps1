param(
    [ValidateSet('build', 'manual', 'bridge', 'train', 'forage')][string]$Mode = 'manual',
    [string]$Editor = '',
    [string]$Python = '',
    [string]$OutputRoot = '',
    [int]$Seed = 1,
    [int]$Steps = 512,
    [ValidateRange(1, 2147483647)][int]$TrainSteps = 2048,
    [string]$ResumeFrom = '',
    [switch]$NewExperiment,
    [string]$BackupRoot = '',
    [string]$HistoryRoot = '',
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
if (($NewExperiment -or $BackupRoot -or $HistoryRoot) -and $Mode -ne 'train') { throw 'NewExperiment、BackupRoot 與 HistoryRoot 僅適用於 train。' }
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
    if ($NewExperiment -and $ResumeFrom) { throw 'NewExperiment 與 ResumeFrom 不可同時使用。' }
    $arguments = @((Join-Path $root 'training/session.py'), '--player', $player, '--output', $OutputRoot, '--steps', $TrainSteps)
    if ($WorldConfig) { $arguments += @('--config', $config) }
    if ($PSBoundParameters.ContainsKey('Seed')) { $arguments += @('--seed', $Seed) }
    if ($ResumeFrom) { $arguments += @('--resume', $ResumeFrom) }
    if ($NewExperiment) { $arguments += '--new' }
    if ($BackupRoot) { $arguments += @('--backup', $BackupRoot) }
    if ($HistoryRoot) { $arguments += @('--history', $HistoryRoot) }
    if ($Visible) { $arguments += '--visible' }
    & $Python @arguments
    if ($LASTEXITCODE -ne 0) { throw '訓練或保存失敗，請查看上述錯誤。' }
    exit 0
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
