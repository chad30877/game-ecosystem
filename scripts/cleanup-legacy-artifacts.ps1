[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)][string]$Plan,
    [string]$PlanSha256 = '',
    [switch]$Apply,
    [switch]$Verify
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Apply -and $Verify) { throw 'Apply 與 Verify 不可同時使用。' }
$workspaceRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactRoot = Join-Path $workspaceRoot 'artifacts'
$planPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Plan)
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
    if ($Relative.Contains(':')) { throw "不接受替代資料流路徑：$Relative" }
    $full = [IO.Path]::GetFullPath((Join-Path $workspaceRoot $Relative))
    if (-not $full.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "目標越界：$full" }
    if ($full -eq $planPath) { throw '不能將清單本身列為清理目標。' }
    $group = $full.Substring($artifactRoot.Length + 1).Split([IO.Path]::DirectorySeparatorChar)[0]
    if ($group -in @('cache', 'migration', 'downloads') -or $group -match '^cleanup($|-)') { throw "保護範圍不納入手動清理：$full" }
    if ([IO.Path]::GetExtension($full).ToLowerInvariant() -in @('.pt', '.onnx', '.ckpt', '.safetensors')) { throw "模型不納入這個清理流程：$full" }
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
if (-not $planPath.StartsWith($workspaceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '清單須位於本專案內。' }
if (-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'AGENTS.md') -PathType Leaf) -or -not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'training/recording.py') -PathType Leaf)) { throw '請將腳本留在本專案 scripts 目錄。' }
$planBytes = [IO.File]::ReadAllBytes($planPath)
$planHasher = [Security.Cryptography.SHA256]::Create()
try { $planHash = [BitConverter]::ToString($planHasher.ComputeHash($planBytes)).Replace('-', '').ToLowerInvariant() }
finally { $planHasher.Dispose() }
if (($effectiveApply -or $Verify) -and -not $PlanSha256) { throw '請先預覽，套用或驗證時以 PlanSha256 指定預覽所得的清單雜湊。' }
if ($PlanSha256 -and ($PlanSha256 -notmatch '^[0-9a-fA-F]{64}$' -or $planHash -ne $PlanSha256)) { throw '清單已變更或 SHA-256 不符，請重新盤點與預覽。' }
$manifest = [Text.UTF8Encoding]::new($false, $true).GetString($planBytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
$count = @($manifest.files).Count
if ($manifest.schema -ne 1 -or $count -lt 1 -or $manifest.count -ne $count -or [long]$manifest.bytes -lt 0) { throw '清單格式、數量或總位元組數無效。' }
$knownPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$totalBytes = [long]0
foreach ($entry in $manifest.files) {
    if ([long]$entry.bytes -lt 0 -or [long]$entry.modifiedUnixMs -lt 0 -or $entry.sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw '清單項目缺少有效的大小、時間或 SHA-256。' }
    $full = Get-Target $entry.path
    if (-not $knownPaths.Add($full)) { throw "清單有重複的實際路徑：$full" }
    $totalBytes += [long]$entry.bytes
}
if ($totalBytes -ne [long]$manifest.bytes) { throw '清單總位元組數與各項加總不符。' }
Assert-NoPlayer
$reportDirectory = Join-Path $artifactRoot ("cleanup/" + $planHash.Substring(0, 16))
Assert-NoLink $reportDirectory
[IO.Directory]::CreateDirectory($reportDirectory) | Out-Null
$stamp = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N').Substring(0, 12))
$reportPath = Join-Path $reportDirectory ("manual-$mode-$stamp.jsonl")
$script:audit = [IO.StreamWriter]::new([IO.FileStream]::new($reportPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read), [Text.UTF8Encoding]::new($false))
$deleted = 0
$deletedBytes = [long]0
$alreadyAbsent = 0
try {
    Write-Audit @{ kind = 'start'; mode = $mode; plan = $planPath; planSha256 = $planHash; count = $count; bytes = $totalBytes; startedAt = [DateTime]::UtcNow.ToString('O') }
    $candidates = @()
    $candidateBytes = [long]0
    $index = 0
    foreach ($entry in $manifest.files) {
        $index++
        Write-Progress -Activity '核對清單' -Status "$index / $count" -PercentComplete ($index * 100 / $count)
        $full = Get-Target $entry.path
        if (-not [IO.File]::Exists($full)) {
            $alreadyAbsent++
            Write-Audit @{ kind = 'already_absent'; path = $entry.path }
            continue
        }
        if (-not $Verify) { Assert-Unchanged $entry $full }
        $reason = if ($entry.PSObject.Properties.Name -contains 'reason') { [string]$entry.reason } else { '既有已盤點清單' }
        $candidates += [pscustomobject]@{ Path = $entry.path; Bytes = [long]$entry.bytes; Reason = $reason; Full = $full; Entry = $entry }
        $candidateBytes += [long]$entry.bytes
        Write-Audit @{ kind = 'candidate'; path = $entry.path; bytes = [long]$entry.bytes; sha256 = $entry.sha256 }
    }
    Write-Progress -Activity '核對清單' -Completed
    # 先核對整批，再開始第一筆刪除；發現異動時不進入刪除階段。
    $candidates | Select-Object Path, Bytes, Reason | Format-Table -AutoSize | Out-Host
    Write-Host "清單 SHA-256：$planHash"
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
    foreach ($entry in $manifest.files) {
        $full = Get-Target $entry.path
        if ([IO.File]::Exists($full)) { $remaining++ }
    }
    $complete = ($Verify -or $effectiveApply) -and $remaining -eq 0
    Write-Audit @{ kind = 'summary'; mode = $mode; planSha256 = $planHash; complete = $complete; total = $count; deleted = $deleted; deletedBytes = $deletedBytes; alreadyAbsent = $alreadyAbsent; remaining = $remaining; stoppedAt = [DateTime]::UtcNow.ToString('O') }
    Write-Host "稽核紀錄：$reportPath"
    if (($Verify -or $effectiveApply) -and -not $complete) { throw "尚有 $remaining 份清單檔案，清理未完成。" }
    if ($complete) { Write-Host "已確認 $count 份清單檔案均不存在。請將稽核紀錄交給 LLM 核對並整理結果。" }
    else { Write-Host '只有預覽，未刪除檔案。確認後手動執行相同腳本，加上 -Apply 與 -PlanSha256。' }
    [pscustomobject]@{ Plan = $planPath; PlanSha256 = $planHash; AuditPath = $reportPath; Mode = $mode; Total = $count; Remaining = $remaining; Complete = $complete }
} catch {
    Write-Audit @{ kind = 'failed'; mode = $mode; message = $_.Exception.Message; deleted = $deleted; deletedBytes = $deletedBytes; at = [DateTime]::UtcNow.ToString('O') }
    [Console]::Error.WriteLine("清理未完成；稽核紀錄：$reportPath")
    throw
} finally {
    Write-Progress -Activity '核對清單' -Completed
    $script:audit.Dispose()
}
