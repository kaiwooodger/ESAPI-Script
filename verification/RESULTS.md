# Verification results

Executed 2026-09-16 Australia/Sydney (2026-09-15 UTC) on macOS ARM64 using Microsoft .NET SDK 8.0.425.

Source SHA256:

```text
3177e1d9c583e4c850f6691e30a4ae3360e4aaa63b8e08a5025b7e0598fed7af
```

## Completed

| Check | Result | Meaning |
| --- | --- | --- |
| Compile and execute deployed geometry with C# 5 language setting | PASS, exit 0 | Same source file, Eclipse adapter excluded with `GEOMETRY_ONLY` |
| Geometry regression suite | 18 passed, 0 failed | Spacing, contours, centroid, containment predicates, retries, end fill and failure propagation |
| Analytic phantom | 13 vertices | 50 mm target radius, 5 mm contraction, 15 mm diameter, 35 mm CTC; regular lattice |
| Full source adapter syntax compilation | PASS, exit 0; 0 warnings/errors | Non-operational API/WPF signatures; not real Varian assemblies |

Raw evidence: [geometry output](geometry-output.txt), [adapter compile output](adapter-compile-output.txt). The regression suite includes fine-grid sphere volume convergence below 0.1% relative error; this is a geometric integration check, not an Eclipse volume result.

## Not completed

| Check | Status | Reason |
| --- | --- | --- |
| Compile against installed Varian assemblies | NOT RUN | Assemblies unavailable on this Mac |
| Execute within Eclipse | NOT RUN | Eclipse unavailable |
| ESAPI contour rasterization and Boolean operations | NOT RUN | Requires the actual TPS |
| Realized sphere volumes/centroids, clinical acceptance | NOT RUN | Requires Eclipse phantom validation and local review |
| Removal rollback behavior under actual ESAPI failures | NOT RUN | Requires TPS execution |

The present evidence verifies the geometry engine. It does not justify saying the current script has already worked in Eclipse. [Compile-Eclipse.ps1](Compile-Eclipse.ps1) and [installation verification](../docs/ECLIPSE_VERIFICATION.md) supply the remaining executable and review procedure. A successful TPS run displays its own copyable verification report with complete-segment containment and requested-centre spacing checks.
