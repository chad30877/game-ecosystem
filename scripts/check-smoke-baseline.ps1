param([string]$Base = 'fa55e382fd10d52e2f31144cbe1f24dfdcd1369e', [string]$Fixture = 'artifacts/smoke-validation/check')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixturePath = (Resolve-Path -LiteralPath $Fixture).Path
$generated = [IO.Path]::GetFullPath((Join-Path $root 'unity/Assets/Editor/SmokeBaselineValidation'))
$allowed = [IO.Path]::GetFullPath((Join-Path $root 'unity/Assets/Editor')) + [IO.Path]::DirectorySeparatorChar
if (-not $generated.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw '基準原始碼路徑超出專案。' }
if (Test-Path -LiteralPath $generated) { throw '基準暫存資料夾已存在，請先確認前次執行狀態。' }
New-Item -ItemType Directory $generated | Out-Null
try {
    foreach ($name in @('World', 'CombatRules', 'ControlEffects', 'SkillRules', 'ForagingController')) {
        $source = (& git -C $root show "${Base}:unity/Assets/Scripts/$name.cs") -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "無法讀取基準 $Base" }
        [IO.File]::WriteAllText((Join-Path $generated "$name.cs"), $source.Replace('namespace Ecosystem', 'namespace SmokeBaseline'), [Text.UTF8Encoding]::new($false))
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SmokeBaselineValidation.cs') -Destination $generated
    $log = Join-Path $root 'artifacts/smoke-validation/baseline-compare.log'
    $process = Start-Process -FilePath (Join-Path $root '.tools/unity/6000.0.85f1/Editor/Unity.exe') -ArgumentList @('-batchmode', '-quit', '-projectPath', ('"{0}"' -f (Join-Path $root 'unity')), '-executeMethod', 'Ecosystem.Editor.SmokeBaselineValidation.Run', '--smoke-fixture', ('"{0}"' -f $fixturePath), '-logFile', ('"{0}"' -f $log)) -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "基準對照失敗：$log" }
    Write-Output $log
} finally {
    # 路徑已確認限制於專案；保留量測原始碼到不參與編譯的證據目錄。
    $saved = Join-Path $root ('artifacts/smoke-validation/baseline-source-' + [guid]::NewGuid().ToString('N'))
    Move-Item -LiteralPath $generated -Destination $saved
    if (Test-Path -LiteralPath "$generated.meta") { Move-Item -LiteralPath "$generated.meta" -Destination "$saved.meta" }
}
