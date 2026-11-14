param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskToolRoot = Join-Path $env:LOCALAPPDATA 'PortfolioTools'
if ($env:PHOTOBENCH_TOOLS) { $taskToolRoot = $env:PHOTOBENCH_TOOLS }
if ($taskToolRoot -match '[\s,]') { throw 'Select a PHOTOBENCH_TOOLS directory without spaces or commas.' }
$taskPythonVersion = & $Python -c 'import platform; print(platform.python_version())'
if ($LASTEXITCODE -ne 0 -or $taskPythonVersion -ne '3.12.7') { throw 'Supply Python3.12.7 using -Python C:\Path\python.exe.' }
foreach ($taskTool in (Get-Content -LiteralPath "$PSScriptRoot\tool-catalog.json" -Raw | ConvertFrom-Json)) {
    $taskDestination = Join-Path $taskToolRoot $taskTool.directory
    New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
    $taskArchive = Join-Path $taskDestination $taskTool.archive
    if (-not (Test-Path -LiteralPath $taskArchive)) {
        Write-Output "Downloading $($taskTool.name) from its official distributor..."
        Invoke-WebRequest -Uri $taskTool.url -OutFile $taskArchive
    }
    $taskHash = (Get-FileHash -LiteralPath $taskArchive -Algorithm $taskTool.algorithm).Hash.ToLowerInvariant()
    if ($taskHash -ne $taskTool.hash) { throw "Archive hash mismatch: $($taskTool.name). No extraction performed." }
    if (-not (Test-Path -LiteralPath (Join-Path $taskDestination $taskTool.executable))) {
        Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskDestination
    }
    Write-Output "Verified $($taskTool.name): $taskHash"
}
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot '.venv\Scripts\python.exe'))) {
    & $Python -m venv (Join-Path $taskRoot '.venv')
    if ($LASTEXITCODE -ne 0) { throw 'Virtual environment creation failed.' }
}
& (Join-Path $taskRoot '.venv\Scripts\python.exe') -m pip install --index-url https://pypi.org/simple -r (Join-Path $taskRoot 'tools-requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Pinned build-tool installation failed.' }
. "$PSScriptRoot\tools.ps1"
Push-Location $taskRoot
try {
    & $taskDotnet restore PhotoBench.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked NuGet restore failed.' }
}
finally { Pop-Location }
