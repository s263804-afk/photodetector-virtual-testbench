. "$PSScriptRoot\tools.ps1"
Push-Location $taskProjectRoot
try {
    & "$PSScriptRoot\build-host-c.ps1"
    & "$PSScriptRoot\build-firmware.ps1"
    & $taskDotnet restore PhotoBench.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    & $taskDotnet test host/PhotoBench.Tests --no-restore --logger 'trx;LogFileName=verification.trx' --results-directory reports/evidence/test-results
    if ($LASTEXITCODE -ne 0) { throw 'NET unit/ARM integration tests failed.' }
    & $taskDotnet build PhotoBench.sln --no-restore -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    & "$PSScriptRoot\demo.ps1"
}
finally { Pop-Location }
