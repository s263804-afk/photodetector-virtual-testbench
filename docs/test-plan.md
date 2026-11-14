# Acceptance and evidence

All test limits describe the stated synthetic model, not physical device specifications.

| Group | Actual ARM input/results | Required limit |
| --- | --- | --- |
| dark | 1024 samples at0uW | code124, no rail flag, baseline within1LSB |
| light_step | 64x124 then64x869 | first changed output170, sixteenth869 |
| linearity | 256 each at0/5/10/20/30/40uW | slope0.060±0.0005V/uW; intercept0.100±0.002V |
| noise | saved1024 samples at20uW | mean/sample-std errors≤1e-6V vs Python reference |
| saturation | 256 samples at60uW | 4095 and high flag on every sample; exclude point from fit |
| communication_recovery | real TCP delivery faults | all required checks pass, unknown operations invalidate session |

Every returned raw/filtered/flag tuple is compared with a direct-window reference.
Linearity uses settled filtered means after15 startup points. Noise reports1024
raw and1009 settled filtered values, n-1 standard deviation, and exact fixture hash.
Model spacing1ms is not a wall-clock sampling-rate measurement.

C unit tests cover invalid batches before mutation, filter rollover, protocol
boundaries, semantic errors, sequence consumption and65535→1 wrap. NET unit tests
cover independent statistics, schema completeness, retries and cancellation.
ARM tests start the project ELF and use the actual C# NetworkStream transport.
The fault proxy only changes byte delivery; it never computes firmware results.
A missing/failed/interrupted group cannot yield overallPASS. Test logs retain the
RED→GREEN history; the example report contains the latest complete demo.
