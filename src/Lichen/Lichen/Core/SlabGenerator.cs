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
            double facadeDepth)
        {
            if (doc == null || volume == null || options == null)
                return;

            if (!options.GenerateFloorSlabs && !options.GenerateCeilingSlabs)
                return;

            double mmToModel = RhinoMath.UnitScale(
                UnitSystem.Millimeters,
                doc.ModelUnitSystem);

            if (options.GenerateFloorSlabs)
            {
                double thickness = options.FloorThicknessMm * mmToModel;
                CreateSlabsAtExtreme(doc, volume, thickness, facadeDepth, isFloor: true);
            }

            if (options.GenerateCeilingSlabs)
            {
                double thickness = options.CeilingThicknessMm * mmToModel;
                CreateSlabsAtExtreme(doc, volume, thickness, facadeDepth, isFloor: false);
            }
        }

        private static void CreateSlabsAtExtreme(
            RhinoDoc doc,
            Brep volume,
            double thickness,
            double facadeDepth,
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
                Brep baseSurface = face.DuplicateFace(false);
                if (baseSurface == null || baseSurface.Faces.Count == 0)
                    continue;

                // Move the slab base down by the facade module depth first so
                // the slab does not overlap the facade modules.
                if (facadeDepth > 0.0)
                {
                    baseSurface.Transform(
                        Transform.Translation(0.0, 0.0, -facadeDepth));
                }

                BrepFace baseFace = baseSurface.Faces[0];
                Vector3d faceNormal = GetOrientedNormal(baseFace);
                double directionSign = Math.Sign(faceNormal * -Vector3d.ZAxis);
                if (directionSign == 0.0)
                    continue;

                // Floors and ceilings both generate downward.
                Brep slab = Brep.CreateFromOffsetFace(
                    baseFace,
                    thickness * directionSign,
                    doc.ModelAbsoluteTolerance,
                    bothSides: false,
                    createSolid: true);

                if (slab == null)
                {
                    RhinoApp.WriteLine(
                        "Lichen: failed to create {0} slab from one face.",
                        isFloor ? "floor" : "ceiling");
                    continue;
                }

                doc.Objects.AddBrep(slab, attributes);
                created++;
            }

            RhinoApp.WriteLine(
                "Lichen: created {0} {1} slab object(s).",
                created,
                isFloor ? "floor" : "ceiling");
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