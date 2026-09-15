# Eclipse installation verification

Status: NOT RUN. No Eclipse installation, Varian DLLs or patient/test RTSTRUCT is available on the development host.

Record Eclipse version, ESAPI DLL versions, workstation framework, source SHA256, image spacing/directions, target resolution, parameters and the run report for every case. These records are local verification data; do not upload patient information to this repository.

## Compile and basic execution

Run `verification/Compile-Eclipse.ps1` against installed assemblies. Require exit code zero. Run the source or compiled plug-in in an editable test structure set. Require a report window with no residual `LAT_TMP`, `LAT_OUT` or `LAT_IN`. Verify the original target contours and volume are unchanged. Record elapsed time and number of structures.

## Analytic phantom

Use an axis-aligned image with fine spacing (e.g. 1 mm) and enough field to contain a 100 mm diameter spherical target centred on a CT plane. Use diameter 15 mm, CTC 35 mm, contraction 5 mm, `FillEndSlices=false`, `RetryInitialSlice=false`.

The ideal analytic geometry produces 13 vertices: the centre plus 12 nearest neighbours at distance 35 mm. Each neighbour's sphere remains 2.5 mm inside the ideal contracted target boundary. Voxelized phantom geometry may change acceptance; resolve any difference using independent contours and segment checks rather than assuming the ideal count must hold on every rasterization.

Export or copy requested centres from the report. Independently calculate every pair distance and require at least 35 mm within numerical rounding. For each realized vertex, compare ESAPI centre of volume against requested coordinates; quantify error versus the image sampling. Inspect axial/sagittal/coronal contours. Compare realized volume to the analytic 1.767146 cc, accounting for voxelization. Establish a local volume/centroid tolerance from phantom resolution before acceptance; this repository does not invent a universal clinical tolerance.

## Containment and union

Independently create `GTV_lattice` contracted by 5 mm in Eclipse. For each vertex and the union, perform Boolean subtraction against that contracted structure; require empty output. Check no clipping or protruding segments. Independently union the individual vertices and subtract in both directions from `LAT_ALL`; require both empty.

## Edge cases

| Case | Required result |
| --- | --- |
| Concave target, hole, separate target components | Every complete sphere inside contracted segment; no centre-only acceptance |
| Thin target; no vertex fits | No retained outputs; clear zero-fit result |
| Exactly one sphere fits | No retained lattice; clear one-fit result |
| Initial phase fails but nearby slice fits | Retry shift recorded; valid complete spheres |
| End fill enabled | End additions retain every-pair CTC; compare with disabled run |
| Reversed image directions | Correct physical slice locations and contours |
| Oblique/in-plane rotated image | Rejected before modifications |
| Standard and high-resolution target | Segment resolution matches; no Boolean resolution errors |
| Missing target / invalid diameter or spacing | Rejected before modifications |
| Existing output ID | Rejected before modifications; existing structures unchanged |
| Fit/vertex resource limit | Clear failure; no retained created structures |
| Structure editing/removal failure | Clear error; any cleanup failure IDs listed |

## Acceptance record

- Source SHA256:
- Eclipse / ESAPI / framework versions:
- Phantom identifier (non-patient):
- Image dimensions / spacing / directions:
- Target resolution:
- Compile result:
- Phantom count / minimum requested-centre spacing:
- Realized volume / centroid errors:
- Containment and union differences:
- Edge cases and cleanup results:
- Reviewer / date / local acceptance decision:

A successful local geometry test report is not a substitute for these checks.
