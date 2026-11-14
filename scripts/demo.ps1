param([string]$Output = 'reports/example', [int]$Port = 12345)
. "$PSScriptRoot\tools.ps1"
Push-Location $taskProjectRoot
try {
    & $taskDotnet run --project host/PhotoBench.Cli --no-restore -- run --scenario all --transport tcp --host 127.0.0.1 --port $Port --output $Output --manage-simulator
    if ($LASTEXITCODE -ne 0) { throw 'The required scenario report did not pass. Inspect report.json.' }
}
finally { Pop-Location }
