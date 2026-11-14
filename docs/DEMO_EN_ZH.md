# Three-minute English demo / 英文演示

This is my independent portfolio project, developed with AI assistance. It tests
an educational photodetector readout workflow. The C firmware is compiled for
ARM Cortex-M4 and executes in Renode. The C# host communicates through the
emulated UART using a loopback TCP connection.

这是我的个人独立项目，使用AI辅助开发。C固件运行ARM指令，C#通过虚拟UART通信。

The input codes are synthetic. They enter a software FEED boundary, not a
simulated ADC peripheral. The model assumes0.6A/W responsivity,100kilohms gain
and0.1volt offset. At10microwatts it produces about0.7volts, or code869.

输入是模型数字码，不经过ADC外设；10微瓦对应约0.7伏、数字码869。

I run scripts/demo.ps1. The runner checks dark baseline, a light step, linearity,
noise, saturation and communication recovery. Every returned sample is compared
with a separate direct-window calculation. Noise uses sample standard deviation;
raw and settled filtered values are reported separately.

演示六组测试；每个样本独立校验，原始噪声与稳态滤波噪声分别统计。

The fault tests drop real reply bytes. Retrying FEED must return cached data
without processing samples twice. If all retries fail, the outcome is unknown:
the host closes the session and requires a new HELLO. A reset can keep TCP open,
so the firmware's ERR_SESSION response matters more than socket connectivity.

重试依靠缓存避免重复处理；重试耗尽不能假定命令未执行；复位也不一定断开TCP。

The report contains the ELF hash, fixture hashes, configuration, session IDs,
limits and observed results. These results validate the virtual workflow. Real
ADC/DMA, analog design, physical serial links and instrument calibration remain
future hardware work. AI helped implement and test the project; my personal
learning record tracks the parts I can independently explain and modify.

报告可追溯；实物验证仍待后续。本人能讲解和修改的内容单独验收。
