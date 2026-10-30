[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [switch]$Apply,
    [switch]$Verify
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Apply -and $Verify) { throw 'Apply 與 Verify 不可同時使用。' }
$workspaceRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactRoot = Join-Path $workspaceRoot 'artifacts'
$planPath = Join-Path $PSScriptRoot 'legacy-artifacts-20261030.json'
$expectedPlanHash = 'c30a946d8c87c33c1181752b485f659a1783c8b4f86de7f3a3e259d5a32c1f7c'
$effectiveApply = $Apply.IsPresent -and -not [bool]$WhatIfPreference
$mode = if ($Verify) { 'verify' } elseif ($effectiveApply) { 'apply' } else { 'preview' }

function Assert-NoLink([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        try {
            $attributes = [IO.File]::GetAttributes($cursor)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "拒絕連結或 junction：$cursor" }
        } catch [IO.FileNotFoundException] {
            # 缺少目標仍須繼續核對其既存祖先。
        } catch [IO.DirectoryNotFoundException] {
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
}

function Get-Target([string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw "清單不是相對路徑：$Relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $workspaceRoot $Relative))
    if (-not $full.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "目標越界：$full" }
    Assert-NoLink $full
    if ([IO.Directory]::Exists($full)) { throw "目標是目錄，不處理：$full" }
    return $full
}

function Assert-NoPlayer {
    if (Get-Process -Name World -ErrorAction SilentlyContinue) { throw '仍有 World Player 執行或播放，請先正常結束後重試。' }
}

function Assert-Unchanged($Entry, [string]$Full) {
    # 以獨占讀取確認檔案未被使用；所有刪除都在這個檢查之後。
    $stream = [IO.File]::Open($Full, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        if ($stream.Length -ne [long]$Entry.bytes) { throw "大小已變更：$Full" }
        $modified = ([DateTimeOffset][IO.File]::GetLastWriteTimeUtc($Full)).ToUnixTimeMilliseconds()
        if ([Math]::Abs($modified - [long]$Entry.modifiedUnixMs) -gt 2) { throw "修改時間已變更：$Full" }
        $actual = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        if ($actual -ne $Entry.sha256) { throw "SHA-256 不符：$Full" }
    } finally { $hash.Dispose(); $stream.Dispose() }
}

$script:audit = $null
function Write-Audit($Value) {
    $script:audit.WriteLine(($Value | ConvertTo-Json -Depth 8 -Compress))
    $script:audit.Flush()
    $script:audit.BaseStream.Flush($true)
}

Assert-NoLink $workspaceRoot
Assert-NoLink $planPath
if (-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'AGENTS.md') -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'training/recording.py') -PathType Leaf)) { throw '請將腳本留在本專案 scripts 目錄。' }
$planStream = [IO.File]::OpenRead($planPath)
$planHasher = [Security.Cryptography.SHA256]::Create()
try { $planHash = [BitConverter]::ToString($planHasher.ComputeHash($planStream)).Replace('-', '').ToLowerInvariant() }
finally { $planHasher.Dispose(); $planStream.Dispose() }
if ($planHash -ne $expectedPlanHash) { throw '清理清單的 SHA-256 不符，拒絕執行。' }
$plan = Get-Content -LiteralPath $planPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($plan.schema -ne 1 -or $plan.count -ne 384 -or @($plan.files).Count -ne 384 -or [long]$plan.bytes -ne 24032917856) { throw '不是這次核定的 384 份清單。' }
if (@($plan.files.path | Sort-Object -Unique).Count -ne 384) { throw '清單有重複路徑。' }
Assert-NoPlayer
$reportDirectory = Join-Path $artifactRoot 'cleanup-20261030'
Assert-NoLink $reportDirectory
[IO.Directory]::CreateDirectory($reportDirectory) | Out-Null
$stamp = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N'))
$reportPath = Join-Path $reportDirectory ("manual-$mode-$stamp.jsonl")
$script:audit = [IO.StreamWriter]::new([IO.FileStream]::new($reportPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read), [Text.UTF8Encoding]::new($false))
$deleted = 0
$deletedBytes = [long]0
$alreadyAbsent = 0
try {
    Write-Audit @{ kind = 'start'; mode = $mode; planSha256 = $expectedPlanHash; count = 384; bytes = [long]$plan.bytes; startedAt = [DateTime]::UtcNow.ToString('O') }
    $candidates = @()
    $candidateBytes = [long]0
    $index = 0
    foreach ($entry in $plan.files) {
        $index++
        Write-Progress -Activity '核對固定清單' -Status "$index / 384" -PercentComplete ($index * 100 / 384)
        $full = Get-Target $entry.path
        if (-not [IO.File]::Exists($full)) {
            $alreadyAbsent++
            Write-Audit @{ kind = 'already_absent'; path = $entry.path }
            continue
        }
        if (-not $Verify) { Assert-Unchanged $entry $full }
        $candidates += [pscustomobject]@{ Path = $entry.path; Bytes = [long]$entry.bytes; Full = $full; Entry = $entry }
        $candidateBytes += [long]$entry.bytes
        Write-Audit @{ kind = 'candidate'; path = $entry.path; bytes = [long]$entry.bytes; sha256 = $entry.sha256 }
    }
    Write-Progress -Activity '核對固定清單' -Completed
    # 先核對整批，再開始第一筆刪除；發現異動時不進入刪除階段。
    $candidates | Select-Object Path, Bytes | Format-Table -AutoSize | Out-Host
    Write-Host ("模式：{0}；符合清單：{1} 份；已不存在：{2} 份；本次候選：{3:N0} bytes" -f $mode, $candidates.Count, $alreadyAbsent, $candidateBytes)
    if ($effectiveApply) {
        foreach ($candidate in $candidates) {
            Assert-NoPlayer
            $full = Get-Target $candidate.Path
            Assert-Unchanged $candidate.Entry $full
            if ($PSCmdlet.ShouldProcess($full, '永久刪除這份已核對的舊模擬檔案')) {
                Write-Audit @{ kind = 'delete_intent'; path = $candidate.Path; bytes = $candidate.Bytes; sha256 = $candidate.Entry.sha256 }
                Remove-Item -LiteralPath $full -Force -ErrorAction Stop
                if ([IO.File]::Exists($full)) { throw "刪除後仍存在：$full" }
                $deleted++
                $deletedBytes += $candidate.Bytes
                Write-Audit @{ kind = 'deleted'; path = $candidate.Path; bytes = $candidate.Bytes }
            }
        }
    }
    $remaining = 0
    foreach ($entry in $plan.files) {
        $full = Get-Target $entry.path
        if ([IO.File]::Exists($full)) { $remaining++ }
    }
    $complete = ($Verify -or $effectiveApply) -and $remaining -eq 0
    Write-Audit @{ kind = 'summary'; mode = $mode; complete = $complete; total = 384; deleted = $deleted; deletedBytes = $deletedBytes; alreadyAbsent = $alreadyAbsent; remaining = $remaining; stoppedAt = [DateTime]::UtcNow.ToString('O') }
    Write-Host "稽核紀錄：$reportPath"
    if (($Verify -or $effectiveApply) -and -not $complete) { throw "尚有 $remaining 份清單檔案，清理未完成。" }
    if ($complete) { Write-Host '已確認 384 份清單檔案均不存在。請回覆 Codex「清理完成，繼續 07、08」。' }
    else { Write-Host '只有預覽，未刪除檔案。確認後手動執行相同腳本並加 -Apply。' }
} catch {
    Write-Audit @{ kind = 'failed'; mode = $mode; message = $_.Exception.Message; deleted = $deleted; deletedBytes = $deletedBytes; at = [DateTime]::UtcNow.ToString('O') }
    [Console]::Error.WriteLine("清理未完成；稽核紀錄：$reportPath")
    throw
} finally {
    Write-Progress -Activity '核對固定清單' -Completed
    $script:audit.Dispose()
}
