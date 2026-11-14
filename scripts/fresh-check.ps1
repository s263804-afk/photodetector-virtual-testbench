param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$taskSource = Split-Path -Parent $PSScriptRoot
$taskFresh = Join-Path $taskSource ('.local\fresh-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskFresh | Out-Null
$taskFiles = & git -C $taskSource ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source checkout.' }
$taskCount = 0
foreach ($taskRelative in $taskFiles) {
    if ($taskRelative -match '^reports/' -and $taskRelative -ne 'reports/tool-versions.json') { continue }
    $taskFrom = Join-Path $taskSource $taskRelative
    if (-not (Test-Path -LiteralPath $taskFrom -PathType Leaf)) { continue }
    $taskTo = Join-Path $taskFresh $taskRelative
    New-Item -ItemType Directory -Path (Split-Path -Parent $taskTo) -Force | Out-Null
    Copy-Item -LiteralPath $taskFrom -Destination $taskTo
    $taskCount++
}
& (Join-Path $taskFresh 'scripts/setup.ps1') -Python $Python *> (Join-Path $taskFresh 'setup.log')
& (Join-Path $taskFresh 'scripts/verify.ps1') *> (Join-Path $taskFresh 'verify.log')
$taskResult = [ordered]@{
    status = 'PASS'; source_files_copied = $taskCount; fresh_checkout = $taskFresh
    scope = 'New source copy, Python venv, C/ARM builds and NET artifacts; reused OS, Python base, hash-verified tool archives and user NuGet cache'
    completed_utc = [DateTime]::UtcNow.ToString('o')
}
$taskResult | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskSource 'reports/evidence/e8-fresh.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $taskFresh 'setup.log') -Destination (Join-Path $taskSource 'reports/evidence/e8-fresh-setup.log')
Copy-Item -LiteralPath (Join-Path $taskFresh 'verify.log') -Destination (Join-Path $taskSource 'reports/evidence/e8-fresh-verification.log')
Get-Content -LiteralPath (Join-Path $taskFresh 'verify.log') -Tail 10
