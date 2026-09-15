# ESAPI Script

Single-file, write-enabled Eclipse plug-in for sphere lattice placement using Gaudreault et al., section 2.3 (DOI [10.1002/mp.16761](https://doi.org/10.1002/mp.16761)). The paper was published online in 2023 and appears in the 2024 journal issue. Scope is lattice segmentation: individual spheres and their union.

**Status: geometry verified locally; Eclipse execution and compilation against real Varian assemblies are not yet verified.** See [verification results](verification/RESULTS.md).

## Review the live source

[src/LatticeSphereGenerator.cs](src/LatticeSphereGenerator.cs) is the complete script. It contains both the tested geometry and the Eclipse adapter. Edit the fields in `LatticePlacement.Settings`:

| Parameter | Default |
| --- | --- |
| TargetId | GTV_lattice |
| VertexDiameterMm | 15 mm |
| CtcMm | 35 mm |
| TargetContractionMm | 5 mm |
| CirclePoints | 96 |
| CreateUnionStructure | true |
| RetryInitialSlice | true |
| FillEndSlices | true |
| OutputPrefix | LAT |

Outputs are CONTROL structures named `LAT_V001`, `LAT_V002`, etc., and `LAT_ALL`. Choose another prefix to retain a different run. IDs remain within 16 characters. Existing output IDs cause a failure before `BeginModifications`; existing structures are never replaced. A run with fewer than two vertices is rejected and its temporary structures are removed.

## Run in Eclipse

1. For the single-file first run, use an editable test structure set on a research/development installation permitting write-enabled ESAPI scripts. A test patient on a clinical database does not itself grant research write access. The API surface targets ESAPI 16.1; the installed Eclipse version and framework must support it.
2. Copy only `src/LatticeSphereGenerator.cs` to the workstation script directory. Edit parameters. Do not copy test stubs into that directory.
3. Load the patient and structure set containing the target, then open Eclipse **Tools → Scripts**, select the script directory and run the `.cs` plug-in. Single-file compilation and approval behavior depend on the installed release. A local compiled DLL alternative is available below.
4. Review the copyable verification window and every generated contour. It lists requested DICOM centre coordinates, ESAPI volumes, minimum requested-centre spacing, retry shift and end-fill count.
5. Complete [Eclipse verification](docs/ECLIPSE_VERIFICATION.md) before accepting the script on that installation. Saving changes remains an Eclipse action.

Compile against your installed Varian DLLs on Windows:

```powershell
.\verification\Compile-Eclipse.ps1 -EsapiDllDirectory "C:\path\to\installed\ESAPI\API"
```

The command compiles a 64-bit `.esapi.dll` into a temporary folder and records source/output hashes. Follow local Eclipse deployment/approval procedures for DLL plug-ins. This script has not been run here because the host is macOS and lacks Varian DLLs.

For a clinical installation, use the compiled binary route: Varian documents approval as mandatory for write-enabled scripts, with approval available for binary plug-ins and stand-alone executables, not single-file source plug-ins. Authorized staff register and approve the binary through **Tools → Script Approvals**. Parameter changes require rebuilding and reviewing the changed binary. See [Varian's approval workflow](https://docs.developer.varian.com/articles/15.6/11_Approving_Scripts_for_Clinical_Use.html).

## Geometry and verification

The lattice starts at the signed-volume centroid of the GTV mesh. Let `d = CTC / sqrt(2)`. Candidate centres are `x = x0 + 2*d*i + d*parity(j+k)`, `y = y0 + d*j`, `z = z0 + d*k`, over bounds-derived integer ranges. Nearest-neighbour distance is CTC; intra-row LR spacing is `sqrt(2)*CTC`. Circles on each CT plane use `sqrt(r*r - dz*dz)`.

ESAPI creates the contracted segment using `Margin(-5)`. Each complete, discretized sphere is accepted only when `sphere.Sub(contracted).IsEmpty`. No clipping is used. All temporary/output structures match the target segment resolution. The original GTV is unchanged. Analytic spheres extending outside the image field or target mesh bounds are rejected before segment acceptance.

Local tests compile the geometry directly from the deployed source with the `GEOMETRY_ONLY` preprocessor symbol:

```sh
dotnet run --project tests/GeometryTests.csproj --configuration Release
dotnet build tests/AdapterCompile.csproj --configuration Release
```

The second command uses deliberately non-operational signature stubs to check adapter C# syntax. It does **not** establish real ESAPI compatibility. GitHub Actions runs the geometry checks on subsequent pushes and pull requests.

## Practical limits

Only axis-aligned axial images are supported; oblique or in-plane rotated axes are rejected. Reversed image directions are accounted for. Diameter must span at least three times the largest voxel spacing. Segment containment checks ESAPI's finite-resolution representation, not a mathematical continuous sphere; realized sphere volume, shape and centroid depend on CT spacing and segmentation. Requested-centre spacing is checked numerically; realized-centroid and boundary checks remain part of Eclipse verification.

The paper does not specify the retry search order, end-slice search order or all details of its boundary fill. This implementation makes those choices deterministic; exact reproduction of the authors' sphere counts is not claimed. See [methodology mapping](docs/METHODOLOGY.md). Resource limits abort oversized runs. Cleanup is best effort because ESAPI has no transaction rollback exposed here; any removal failures are identified in the error message.
