# Verified release checkpoint —14 November2026

Technical verification is separate from personal mastery.

- Host C: CTest3/3; filter245, protocol89 and device221 assertions (555 total).
- NET10:49 passed,0 failed,0 skipped, including actual Renode ARM integration.
- ARM: project firmware cross-compiled with GCC14.3.1, Cortex-M4/Thumb, warnings as errors.
- Release NET build:0 warnings,0 errors.
- CLI: all six groupsPASS;3968 actual UART sample tuples; JSON/CSV/HTML saved.
- Host UBSan probe: compiled and ran the device executable,221 assertionsPASS.
  This applies to host C under Zig/Clang, not the emulated ARM or all configurations.
- Fresh reproduction: separate source copy, venv, C/ARM builds and NET artifacts,
  full49 tests and CLI allPASS. Reused Windows OS, Python base, hash-verified tool
  archives and user NuGet cache; this was not a fresh operating-system installation.
- Remote CI is CONFIGURED_NOT_YET_OBSERVED; local evidence does not imply GitHub CI ran.
- Independent whole-branch review: two Important findings fixed with three RED-to-GREEN regressions; full49/49 and fresh49/49 suites passed.

Evidence: review-full-verification.log, e8-fresh.json, e8-fresh-verification.log,
e8-setup.log, e8-ubsan.log and test-results/verification.trx under reports/evidence.
Earlier RED/failure logs intentionally remain as diagnostic history.

The review fixes preserve results on connection refusal and retain accumulated checks,
session IDs and traces on communication interruption. A later parallel run exposed
Renode history-file sharing; each launch now uses a private config/history file.

Deferred minor: HELLO compatibility uses substring matching, so a different device
advertising protocol=10 or mode=VIRTUAL_FEED_OTHER could be accepted. The tested
project firmware advertises the exact required values. Strict field parsing is deferred.

The actual simulator reset-vector bug and fix are documented in docs/bug-report.md.
Physical hardware, ADC/DMA and measured optical performance remain unverified.
Renode vendor runtimeNET8.0.10 remains a disclosed dependency gap; C# hostNET10.0.15.

Personal learning E1–E8: PENDING_LEARNER, lessons prepared but not delivered/accepted.

## English publication copy

Documentation is published in English. Localized diagnostic labels in selected
archived transcripts/reports were translated or excerpted for publication; these
files are labelled and are not presented as verbatim raw console output. Original
transcripts and bilingual learning materials remain in a separate local backup.
See english-publication.json for original hashes and the affected-file list.
The original English GitHub execution log is evidence/github-ci-verified.log.
Numeric measurements, test outcomes, algorithms and source-data captures are unchanged.
