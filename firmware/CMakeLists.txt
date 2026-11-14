cmake_minimum_required(VERSION 3.25)
project(PhotoBench C)
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
option(PHOTO_ARM "Build ARM firmware rather than portable host tests" OFF)

if(PHOTO_ARM)
  enable_language(ASM)
  add_executable(photo-smoke platform/stm32/startup.S platform/stm32/uart.c platform/stm32/smoke.c)
  add_executable(photo-firmware platform/stm32/startup.S platform/stm32/uart.c
    platform/stm32/main.c core/filter.c core/protocol.c core/device.c)
  target_include_directories(photo-firmware PRIVATE core platform/stm32)
  foreach(firmware_target photo-smoke photo-firmware)
  target_compile_options(${firmware_target} PRIVATE
    -mcpu=cortex-m4 -mthumb -mfloat-abi=soft -Os
    -ffreestanding -ffunction-sections -fdata-sections -Wall -Wextra -Werror)
  target_link_options(${firmware_target} PRIVATE
    -mcpu=cortex-m4 -mthumb -mfloat-abi=soft
    --specs=nano.specs --specs=nosys.specs -nostartfiles
    "-T${CMAKE_CURRENT_SOURCE_DIR}/platform/stm32/linker.ld"
    -Wl,--gc-sections -Xlinker "-Map=${CMAKE_CURRENT_BINARY_DIR}/${firmware_target}.map")
  set_target_properties(${firmware_target} PROPERTIES SUFFIX ".elf")
  endforeach()
else()
  enable_testing()
  add_library(photo-core OBJECT core/filter.c core/protocol.c core/device.c)
  target_include_directories(photo-core PUBLIC core)
  target_compile_options(photo-core PRIVATE -Wall -Wextra -Werror -O2)
  add_executable(test-filter tests/test_filter.c)
  target_link_libraries(test-filter PRIVATE photo-core)
  target_compile_options(test-filter PRIVATE -Wall -Wextra -Werror)
  add_test(NAME filter COMMAND test-filter)
  add_executable(test-protocol tests/test_protocol.c)
  target_link_libraries(test-protocol PRIVATE photo-core)
  target_compile_options(test-protocol PRIVATE -Wall -Wextra -Werror)
  add_test(NAME protocol COMMAND test-protocol)
  add_executable(test-device tests/test_device.c)
  target_link_libraries(test-device PRIVATE photo-core)
  target_compile_options(test-device PRIVATE -Wall -Wextra -Werror)
  add_test(NAME device COMMAND test-device)
endif()
