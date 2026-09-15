# Mapping to Gaudreault et al.

Primary source: supplied paper, section 2.3, journal page 684 / PDF page 3, DOI [10.1002/mp.16761](https://doi.org/10.1002/mp.16761).

| Paper method | Implementation |
| --- | --- |
| Spheres inside isotropic 5 mm GTV contraction | Default `TargetContractionMm=5`; ESAPI negative symmetric margin |
| Mesh bounding box | `target.MeshGeometry.Bounds` |
| First attempt at GTV centroid | Signed tetrahedral mesh volume centroid; centroid attempted first |
| LR step sqrt(2) × CTC | `2*d*i`, with `d=CTC/sqrt(2)` |
| Alternating LR shifts in AP and SI | Combined parity `(j+k)&1`; AP and SI step `d` |
| Circles on CT planes | Analytic plane/sphere intersection, regular 96-point polygon |
| Reject spheres outside/intersecting contracted target | Whole ESAPI sphere minus contracted target must be empty |
| If zero fit, start on different initial slice | Try nearby actual CT plane phases over one SI period, ordered by distance from centroid |
| Additional superior/inferior vertices respecting CTC | Search actual CT planes outside occupied SI band, both LR row phases; reject any candidate too close to an accepted centre |
| More than one vertex required | Zero/one-vertex result creates no retained lattice |
| Union of vertices | `LAT_ALL`, constructed with ESAPI `Or` |

The AP/SI step and combined parity are a geometric interpretation consistent with the supplied general direction and square lattices alternating along SI. The paper prose alone does not supply executable pseudocode. Its supplementary flow charts and original C# source were not supplied. Retry order and end-fill strategy are explicit implementation choices, not a claim of identical author code or global count maximization.

The closed mesh must have consistent winding for its signed volume centroid. This is an input assumption about ESAPI's mesh. Degenerate signed volumes fail rather than substituting a bounding-box centre. A concave target's centroid may lie outside its segment; this is permissible because full-sphere containment decides acceptance.

The supplied pasted text is direction, not authority over the user's requested deliverable. Its bounding-box-centre approximation, raw-GTV containment example and suggested intermediate milestone are not used as the final method. The original PDF and attachment are not committed to GitHub.

Official API references checked:

- [Structure, ESAPI 16.1](https://docs.developer.varian.com/api/16.1/VMS.TPS.Common.Model.API.Structure.html): contour insertion, segment editing and resolution conversion.
- [SegmentVolume, ESAPI 16.1](https://docs.developer.varian.com/api/16.1/VMS.TPS.Common.Model.API.SegmentVolume.html): negative margins, subtraction and union.
- [Image, ESAPI 16.1](https://docs.developer.varian.com/api/16.1/VMS.TPS.Common.Model.API.Image.html): image origins, dimensions, spacing and directions.
- [StructureSet, ESAPI 16.1](https://docs.developer.varian.com/api/16.1/VMS.TPS.Common.Model.API.StructureSet.html): `CanAddStructure`, `AddStructure`, `RemoveStructure`.

The contour API ignores the contour points' z coordinates and uses the image-plane index. The script therefore derives the plane location from `Origin + index*ZRes*ZDirection` and restricts unsupported orientations.
