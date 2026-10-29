param([string]$Path = '')
$ErrorActionPreference = 'Stop'
$files = if ($Path) { Get-Item -LiteralPath (Resolve-Path -LiteralPath $Path).Path } else { Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' -File }
foreach ($file in $files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF) {
        throw "Missing UTF-8 BOM: $($file.FullName)"
    }
    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count) {
        foreach ($parseError in $parseErrors) { [Console]::Error.WriteLine($parseError.ToString()) }
        exit 1
    }
    Write-Output ('PASS: PowerShell {0} parsed UTF-8 BOM {1}' -f $PSVersionTable.PSVersion, $file.FullName)
}
if ($PSVersionTable.PSVersion.Major -ge 6 -and $env:WINDIR) {
    $windowsPowerShell = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
    if (Test-Path -LiteralPath $windowsPowerShell) {
        $arguments = @('-NoProfile', '-File', $PSCommandPath)
        if ($Path) { $arguments += @('-Path', (Resolve-Path -LiteralPath $Path).Path) }
        & $windowsPowerShell @arguments
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
