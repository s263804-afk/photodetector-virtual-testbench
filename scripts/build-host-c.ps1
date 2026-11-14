. "$PSScriptRoot\tools.ps1"
$env:ZIG_GLOBAL_CACHE_DIR = Join-Path $taskProjectRoot '.local\zig-cache'
& $taskCmake -Wno-unused-cli -S (Join-Path $taskProjectRoot 'firmware') -B (Join-Path $taskProjectRoot 'build\host') -G Ninja "-DCMAKE_MAKE_PROGRAM=$taskNinja" "-DCMAKE_C_COMPILER=$taskZig" -DCMAKE_C_COMPILER_ARG1=cc -DPHOTO_ARM=OFF
if ($LASTEXITCODE -ne 0) { throw 'Host C configure failed.' }
& $taskCmake --build (Join-Path $taskProjectRoot 'build\host')
if ($LASTEXITCODE -ne 0) { throw 'Host C build failed.' }
& (Join-Path $taskProjectRoot '.venv\Scripts\ctest.exe') --test-dir (Join-Path $taskProjectRoot 'build\host') --output-on-failure
if ($LASTEXITCODE -ne 0) { throw 'Host C tests failed.' }
