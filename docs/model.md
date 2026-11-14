# Optical stimulus and reference

These constants define an educational synthetic model, not a measured detector:
responsivity 0.6 A/W, transimpedance 100000 ohm, offset 0.100 V, reference 3.300 V.
Power is supplied in microwatts; V = 0.100 + 0.060 P_uW. Clip to [0,3.3], then
code = floor(4095 V / 3.3 + 0.5). Codes are 12-bit unsigned. Nominal spacing is
1 ms of model time; execution wall time does not validate hardware sampling rate.

Noise vector noise-20uw.json was saved with seed20261112, Python3.12 random.gauss,
sigma0.002 V. Its samples, direct-window integer reference and Python statistics
are immutable release inputs, with SHA256 beside the file. C# also provides a
Box-Muller model generator; runtime PRNG implementations are not used as an
interchangeable reference for the saved vector.

ReferenceFilter recomputes each window directly, independently of C's ring buffer.
Statistics use Welford accumulation and n-1 sample standard deviation. Filtered
noise excludes the first15 startup samples, leaving1009 of1024; raw retains1024.
Insufficient data has an explicit status and null deviation. Rail clipping means
exact code0 or4095; configurable near-rail flags are a separate firmware signal.
Clipped power levels are excluded from least-squares fitting, which requires at
least three distinct unclipped levels. Baseline subtraction reports raw mean
minus the measured dark mean. No physical optical calibration is claimed.
