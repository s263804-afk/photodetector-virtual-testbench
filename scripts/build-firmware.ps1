. "$PSScriptRoot\tools.ps1"
& $taskCmake -Wno-unused-cli -S (Join-Path $taskProjectRoot 'firmware') -B (Join-Path $taskProjectRoot 'build\arm') -G Ninja "-DCMAKE_MAKE_PROGRAM=$taskNinja" "-DCMAKE_TOOLCHAIN_FILE=$taskProjectRoot\firmware\cmake\arm-toolchain.cmake" "-DARM_GCC_ROOT=$taskArmRoot" -DPHOTO_ARM=ON
if ($LASTEXITCODE -ne 0) { throw 'ARM CMake configuration failed.' }
& $taskCmake --build (Join-Path $taskProjectRoot 'build\arm')
if ($LASTEXITCODE -ne 0) { throw 'ARM build failed.' }
