using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Lichen.Core
{
    public class SlabGenerationOptions
    {
        public bool GenerateFloorSlabs { get; set; }
        public bool GenerateCeilingSlabs { get; set; }
        public double FloorThicknessMm { get; set; } = 500.0;
        public double CeilingThicknessMm { get; set; } = 500.0;
    }

    public static class SlabGenerator
    {
        private const double HorizontalAngleToleranceDegrees = 15.0;

        public static void GenerateForVolume(
            RhinoDoc doc,
            Brep volume,
            SlabGenerationOptions options,
            double inwardFacadeDepth)
        {
            if (doc == null || volume == null || options == null)
                return;

            if (!options.GenerateFloorSlabs && !options.GenerateCeilingSlabs)
                return;

            if (options.GenerateFloorSlabs)
            {
                double thickness = UnitConverter.MillimetersToModel(
                    doc,
                    options.FloorThicknessMm);

                CreateSlabsAtExtreme(
                    doc,
                    volume,
                    thickness,
                    inwardFacadeDepth,
                    isFloor: true);
            }

            if (options.GenerateCeilingSlabs)
            {
                double thickness = UnitConverter.MillimetersToModel(
                    doc,
                    options.CeilingThicknessMm);

                CreateSlabsAtExtreme(
                    doc,
                    volume,
                    thickness,
                    inwardFacadeDepth,
                    isFloor: false);
            }
        }

        private static void CreateSlabsAtExtreme(
            RhinoDoc doc,
            Brep volume,
            double thickness,
            double inwardFacadeDepth,
            bool isFloor)
        {
            if (thickness <= 0.0)
                return;

            List<BrepFace> faces = GetExtremeHorizontalFaces(
                volume,
                isFloor,
                doc.ModelAbsoluteTolerance);

            if (faces.Count == 0)
            {
                RhinoApp.WriteLine(
                    "Lichen: no suitable {0} face found for slab generation.",
                    isFloor ? "bottom" : "top");
                return;
            }

            int layerIndex = BlockManager.EnsureSlabLayer(doc, isFloor);
            var attributes = new ObjectAttributes { LayerIndex = layerIndex };

            int created = 0;

            foreach (BrepFace face in faces)
            {
                List<Brep> slabBases = CreateInsetPlanarRegions(
                    face,
                    inwardFacadeDepth,
                    doc.ModelAbsoluteTolerance);

                if (slabBases.Count == 0)
                {
                    RhinoApp.WriteLine(
                        "Lichen: failed to create inset {0} slab boundary.",
                        isFloor ? "floor" : "ceiling");
                    continue;
                }

                foreach (Brep slabBase in slabBases)
                {
                    if (slabBase == null || slabBase.Faces.Count == 0)
                        continue;

                    BrepFace baseFace = slabBase.Faces[0];
                    Vector3d faceNormal = GetOrientedNormal(baseFace);
                    double directionSign = Math.Sign(faceNormal * -Vector3d.ZAxis);
                    if (directionSign == 0.0)
                        continue;

                    // Floors and ceilings both extrude downward from their
                    // original horizontal face elevation.
                    Brep slab = Brep.CreateFromOffsetFace(
                        baseFace,
                        thickness * directionSign,
                        doc.ModelAbsoluteTolerance,
                        bothSides: false,
                        createSolid: true);

                    if (slab == null)
                    {
                        RhinoApp.WriteLine(
                            "Lichen: failed to create {0} slab from one inset region.",
                            isFloor ? "floor" : "ceiling");
                        continue;
                    }

                    doc.Objects.AddBrep(slab, attributes);
                    created++;
                }
            }

            RhinoApp.WriteLine(
                "Lichen: created {0} {1} slab object(s).",
                created,
                isFloor ? "floor" : "ceiling");
        }

        private static List<Brep> CreateInsetPlanarRegions(
            BrepFace face,
            double inwardFacadeDepth,
            double tolerance)
        {
            var result = new List<Brep>();

            if (!face.TryGetPlane(out Plane plane, tolerance))
            {
                RhinoApp.WriteLine(
                    "Lichen: slab face is not planar enough to inset its boundary.");
                return result;
            }

            // Use a consistent +Z plane so outer/inner loop orientation can be
            // normalised reliably for both top and bottom faces.
            if (plane.ZAxis * Vector3d.ZAxis < 0.0)
                plane.Flip();

            var boundaries = new List<Curve>();

            foreach (BrepLoop loop in face.Loops)
            {
                Curve boundary = loop.To3dCurve();
                if (boundary == null || !boundary.IsClosed)
                    continue;

                Curve working = boundary.DuplicateCurve();
                if (working == null)
                    continue;

                // With a +Z plane, make the slab region lie on the left side
                // of every loop: outer loops CCW, inner loops CW. A positive
                // offset then erodes the slab region from every trimmed edge.
                CurveOrientation orientation = working.ClosedCurveOrientation(plane);

                if (loop.LoopType == BrepLoopType.Outer &&
                    orientation == CurveOrientation.Clockwise)
                {
                    working.Reverse();
                }
                else if (loop.LoopType == BrepLoopType.Inner &&
                         orientation == CurveOrientation.CounterClockwise)
                {
                    working.Reverse();
                }

                if (inwardFacadeDepth <= tolerance)
                {
                    boundaries.Add(working);
                    continue;
                }

                Curve[] offsets = working.Offset(
                    plane,
                    -inwardFacadeDepth,
                    tolerance,
                    CurveOffsetCornerStyle.Sharp);

                if (offsets == null || offsets.Length == 0)
                {
                    RhinoApp.WriteLine(
                        "Lichen: a slab boundary could not be inset by {0:G6} model units.",
                        inwardFacadeDepth);
                    continue;
                }

                Curve[] joinedOffsets = Curve.JoinCurves(offsets, tolerance);
                if (joinedOffsets == null || joinedOffsets.Length == 0)
                    joinedOffsets = offsets;

                foreach (Curve offset in joinedOffsets)
                {
                    if (offset != null && offset.IsClosed)
                        boundaries.Add(offset);
                }
            }

            if (boundaries.Count == 0)
                return result;

            Brep[] planarRegions = Brep.CreatePlanarBreps(boundaries, tolerance);
            if (planarRegions == null)
                return result;

            foreach (Brep region in planarRegions)
            {
                if (region != null && region.Faces.Count > 0)
                    result.Add(region);
            }

            return result;
        }

        private static List<BrepFace> GetExtremeHorizontalFaces(
            Brep volume,
            bool isFloor,
            double modelTolerance)
        {
            var candidates = new List<BrepFace>();
            var elevations = new List<double>();

            double cosTolerance = Math.Cos(
                HorizontalAngleToleranceDegrees * Math.PI / 180.0);

            foreach (BrepFace face in volume.Faces)
            {
                Vector3d normal = GetOrientedNormal(face);
                if (!normal.Unitize())
                    continue;

                if (Math.Abs(normal * Vector3d.ZAxis) < cosTolerance)
                    continue;

                BoundingBox bbox = face.GetBoundingBox(true);
                if (!bbox.IsValid)
                    continue;

                candidates.Add(face);
                elevations.Add(bbox.Center.Z);
            }

            var result = new List<BrepFace>();
            if (candidates.Count == 0)
                return result;

            double extreme = elevations[0];
            for (int i = 1; i < elevations.Count; i++)
            {
                extreme = isFloor
                    ? Math.Min(extreme, elevations[i])
                    : Math.Max(extreme, elevations[i]);
            }

            double elevationTolerance = Math.Max(modelTolerance * 10.0, 1e-6);

            for (int i = 0; i < candidates.Count; i++)
            {
                if (Math.Abs(elevations[i] - extreme) <= elevationTolerance)
                    result.Add(candidates[i]);
            }

            return result;
        }

        private static Vector3d GetOrientedNormal(BrepFace face)
        {
            Vector3d normal = face.NormalAt(
                face.Domain(0).Mid,
                face.Domain(1).Mid);

            if (face.OrientationIsReversed)
                normal = -normal;

            return normal;
        }
    }
}
