using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Lichen.Core
{
    public static class CornerGenerator
    {
        private const double CornerLegLengthMm = 500.0;
        private const double CornerOffsetMm = 200.0;
        private const double AngleToleranceDegrees = 0.1;
        private const double VerticalEdgeToleranceDegrees = 15.0;
        private const double InsideProbeMm = 50.0;

        public static int GenerateForVolume(
            RhinoDoc doc,
            Brep volume,
            FacadeModule module,
            int facadeBlockDefIndex,
            IDictionary<int, List<FacadeVerticalSpan>> placedSpansByFace)
        {
            if (volume == null || placedSpansByFace == null || placedSpansByFace.Count == 0)
                return 0;

            double modelTolerance = doc.ModelAbsoluteTolerance;
            double insideProbe = UnitConverter.MillimetersToModel(doc, InsideProbeMm);
            int placedCount = 0;

            foreach (BrepEdge edge in volume.Edges)
            {
                int[] adjacentFaces = edge.AdjacentFaces();
                if (adjacentFaces == null || adjacentFaces.Length != 2)
                    continue;

                int faceIndexA = adjacentFaces[0];
                int faceIndexB = adjacentFaces[1];

                if (!placedSpansByFace.TryGetValue(faceIndexA, out List<FacadeVerticalSpan> spansA) ||
                    !placedSpansByFace.TryGetValue(faceIndexB, out List<FacadeVerticalSpan> spansB))
                {
                    continue;
                }

                if (spansA.Count == 0 || spansB.Count == 0)
                    continue;

                Point3d edgeStart = edge.PointAtStart;
                Point3d edgeEnd = edge.PointAtEnd;
                Vector3d edgeVector = edgeEnd - edgeStart;
                if (!edgeVector.Unitize())
                    continue;

                double edgeAngle = Vector3d.VectorAngle(edgeVector, Vector3d.ZAxis) * 180.0 / Math.PI;
                if (edgeAngle > 90.0)
                    edgeAngle = 180.0 - edgeAngle;

                if (edgeAngle > VerticalEdgeToleranceDegrees)
                    continue;

                BrepFace faceA = volume.Faces[faceIndexA];
                BrepFace faceB = volume.Faces[faceIndexB];

                Vector3d normalA = FacadePlacer.GetFacadeNormal(faceA);
                Vector3d normalB = FacadePlacer.GetFacadeNormal(faceB);
                if (!normalA.IsValid || !normalB.IsValid)
                    continue;

                Point3d edgeMid = edge.PointAt(edge.Domain.Mid);
                Point3d cornerBase = edgeStart.Z <= edgeEnd.Z ? edgeStart : edgeEnd;

                Vector3d directionA = GetDirectionIntoFace(faceA, edgeMid, normalA);
                Vector3d directionB = GetDirectionIntoFace(faceB, edgeMid, normalB);
                if (!directionA.Unitize() || !directionB.Unitize())
                    continue;

                OrderDirectionsCounterClockwise(
                    directionA,
                    directionB,
                    out Vector3d firstDirection,
                    out Vector3d secondDirection,
                    out double cornerAngleDegrees);

                if (cornerAngleDegrees < AngleToleranceDegrees ||
                    Math.Abs(180.0 - cornerAngleDegrees) < AngleToleranceDegrees)
                {
                    continue;
                }

                bool isConcave = IsConcaveCorner(
                    volume,
                    edgeMid,
                    firstDirection,
                    secondDirection,
                    insideProbe,
                    modelTolerance);

                List<VerticalOverlap> overlaps = GetVerticalOverlaps(spansA, spansB, modelTolerance);
                foreach (VerticalOverlap overlap in overlaps)
                {
                    double height = overlap.MaxZ - overlap.MinZ;
                    if (height <= modelTolerance)
                        continue;

                    int cornerDefIndex = BlockManager.GetOrCreateCornerPlaceholderDefinition(
                        doc,
                        module,
                        cornerAngleDegrees,
                        isConcave,
                        height,
                        CornerLegLengthMm,
                        CornerOffsetMm);

                    if (cornerDefIndex < 0)
                        continue;

                    BlockManager.EnsureCornerMaster(
                        doc,
                        module,
                        facadeBlockDefIndex,
                        cornerDefIndex);

                    double rotationRadians = Math.Atan2(firstDirection.Y, firstDirection.X);
                    Point3d insertionPoint = new Point3d(cornerBase.X, cornerBase.Y, overlap.MinZ);

                    Transform rotate = Transform.Rotation(
                        rotationRadians,
                        Vector3d.ZAxis,
                        Point3d.Origin);
                    Transform translate = Transform.Translation(insertionPoint - Point3d.Origin);

                    var attributes = new ObjectAttributes
                    {
                        LayerIndex = BlockManager.EnsureCornersLayer(doc)
                    };

                    doc.Objects.AddInstanceObject(
                        cornerDefIndex,
                        translate * rotate,
                        attributes);

                    placedCount++;
                }
            }

            return placedCount;
        }

        private static Vector3d GetDirectionIntoFace(
            BrepFace face,
            Point3d corner,
            Vector3d facadeNormal)
        {
            Vector3d tangent = Vector3d.CrossProduct(Vector3d.ZAxis, facadeNormal);
            tangent.Z = 0.0;
            if (!tangent.Unitize())
                return Vector3d.Unset;

            Point3d faceCentre = face.GetBoundingBox(true).Center;
            Vector3d towardCentre = faceCentre - corner;
            towardCentre.Z = 0.0;

            if (Vector3d.Multiply(towardCentre, tangent) < 0.0)
                tangent = -tangent;

            return tangent;
        }

        private static void OrderDirectionsCounterClockwise(
            Vector3d a,
            Vector3d b,
            out Vector3d first,
            out Vector3d second,
            out double angleDegrees)
        {
            double angleA = Math.Atan2(a.Y, a.X);
            double angleB = Math.Atan2(b.Y, b.X);
            double ccw = angleB - angleA;
            while (ccw < 0.0) ccw += Math.PI * 2.0;
            while (ccw >= Math.PI * 2.0) ccw -= Math.PI * 2.0;

            if (ccw <= Math.PI)
            {
                first = a;
                second = b;
                angleDegrees = ccw * 180.0 / Math.PI;
            }
            else
            {
                first = b;
                second = a;
                angleDegrees = (Math.PI * 2.0 - ccw) * 180.0 / Math.PI;
            }
        }

        private static bool IsConcaveCorner(
            Brep volume,
            Point3d edgeMid,
            Vector3d firstDirection,
            Vector3d secondDirection,
            double probeDistance,
            double tolerance)
        {
            Vector3d bisector = firstDirection + secondDirection;
            bisector.Z = 0.0;
            if (!bisector.Unitize())
                return false;

            Point3d probe = edgeMid + bisector * probeDistance;
            bool smallerSectorIsInside = volume.IsPointInside(probe, tolerance, false);
            return !smallerSectorIsInside;
        }

        private static List<VerticalOverlap> GetVerticalOverlaps(
            List<FacadeVerticalSpan> spansA,
            List<FacadeVerticalSpan> spansB,
            double tolerance)
        {
            var overlaps = new List<VerticalOverlap>();

            foreach (FacadeVerticalSpan a in spansA)
            {
                foreach (FacadeVerticalSpan b in spansB)
                {
                    double minZ = Math.Max(a.MinZ, b.MinZ);
                    double maxZ = Math.Min(a.MaxZ, b.MaxZ);

                    if (maxZ - minZ <= tolerance)
                        continue;

                    bool duplicate = false;
                    foreach (VerticalOverlap existing in overlaps)
                    {
                        if (Math.Abs(existing.MinZ - minZ) <= tolerance &&
                            Math.Abs(existing.MaxZ - maxZ) <= tolerance)
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                        overlaps.Add(new VerticalOverlap(minZ, maxZ));
                }
            }

            return overlaps;
        }

        private readonly struct VerticalOverlap
        {
            public VerticalOverlap(double minZ, double maxZ)
            {
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public double MinZ { get; }
            public double MaxZ { get; }
        }
    }
}
