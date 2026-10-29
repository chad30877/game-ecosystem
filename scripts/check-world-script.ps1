param([string]$Path = (Join-Path $PSScriptRoot 'world.ps1'))
$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path -LiteralPath $Path).Path, [ref]$tokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count) {
    foreach ($parseError in $parseErrors) { [Console]::Error.WriteLine($parseError.ToString()) }
    exit 1
}
Write-Output ('PASS: PowerShell {0} parsed {1}' -f $PSVersionTable.PSVersion, $Path)