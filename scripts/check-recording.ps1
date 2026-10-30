param([switch]$Build)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$editor = Join-Path $root '.tools/unity/6000.0.85f1/Editor/Unity.exe'
$boundary = Join-Path $root 'artifacts/recording-validation/path-boundary'
New-Item -ItemType Directory -Force (Join-Path $boundary 'outside') | Out-Null
[IO.File]::WriteAllText((Join-Path $boundary 'outside/preserve.txt'), 'preserve')
if (-not (Test-Path -LiteralPath (Join-Path $boundary 'alias'))) {
    New-Item -ItemType Junction -Path (Join-Path $boundary 'alias') -Target (Join-Path $boundary 'outside') | Out-Null
}
$log = Join-Path $root ('artifacts/recording-validation/check-{0}.log' -f [guid]::NewGuid().ToString('N'))
$method = if ($Build) { 'Ecosystem.Editor.ValidateArtifacts.AllAndBuild' } else { 'Ecosystem.Editor.ValidateArtifacts.All' }
$process = Start-Process -FilePath $editor -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"{0}"' -f (Join-Path $root 'unity')), '-executeMethod', $method, '-logFile', ('"{0}"' -f $log)) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "記錄契約驗收失敗，請查看 $log" }
Write-Output $log
