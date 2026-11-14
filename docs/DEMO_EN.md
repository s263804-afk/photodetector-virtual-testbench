# Three-minute English demo

This is my independent portfolio project, developed with AI assistance. It tests
an educational photodetector readout workflow. The C firmware is compiled for
ARM Cortex-M4 and executes in Renode. The C# host communicates through the
emulated UART using a loopback TCP connection.

The input codes are synthetic. They enter a software FEED boundary, not a
simulated ADC peripheral. The model assumes0.6A/W responsivity,100kilohms gain
and0.1volt offset. At10microwatts it produces about0.7volts, or code869.

I run scripts/demo.ps1. The runner checks dark baseline, a light step, linearity,
noise, saturation and communication recovery. Every returned sample is compared
with a separate direct-window calculation. Noise uses sample standard deviation;
raw and settled filtered values are reported separately.

The fault tests drop real reply bytes. Retrying FEED must return cached data
without processing samples twice. If all retries fail, the outcome is unknown:
the host closes the session and requires a new HELLO. A reset can keep TCP open,
so the firmware's ERR_SESSION response matters more than socket connectivity.

The report contains the ELF hash, fixture hashes, configuration, session IDs,
limits and observed results. These results validate the virtual workflow. Real
ADC/DMA, analog design, physical serial links and instrument calibration remain
future hardware work. AI helped implement and test the project; my personal
learning record tracks the parts I can independently explain and modify.
