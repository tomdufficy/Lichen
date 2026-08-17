using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;

namespace Lichen.Core
{
    public static class GapGenerator
    {
        public static int GenerateForFace(
            RhinoDoc doc,
            FacadeModule module,
            int facadeBlockDefIndex,
            FacadePlacementResult placement,
            double depthInside,
            double depthOutside,
            bool includeEdgeGaps)
        {
            if (placement == null ||
                placement.HorizontalGaps.Count == 0 ||
                placement.VerticalSpans.Count == 0)
            {
                return 0;
            }

            int placedCount = 0;
            double tolerance = doc.ModelAbsoluteTolerance;

            Plane sourcePlane = Plane.WorldXY;
            Vector3d tX = placement.XAxis;
            Vector3d tY = placement.Normal;
            Vector3d tZ = Vector3d.CrossProduct(tX, tY);
            if (tZ * Vector3d.ZAxis < 0)
            {
                tZ = -tZ;
                tY = -tY;
            }

            Plane targetPlane = new Plane(
                Point3d.Origin,
                tX,
                Vector3d.CrossProduct(tZ, tX));
            Transform orient = Transform.PlaneToPlane(sourcePlane, targetPlane);

            var attributes = new ObjectAttributes
            {
                LayerIndex = BlockManager.EnsureGapsLayer(doc)
            };

            foreach (FacadeHorizontalGap gap in placement.HorizontalGaps)
            {
                if (gap.Width <= tolerance)
                    continue;

                if (gap.IsEdge && !includeEdgeGaps)
                    continue;

                foreach (FacadeVerticalSpan span in placement.VerticalSpans)
                {
                    double height = span.MaxZ - span.MinZ;
                    if (height <= tolerance)
                        continue;

                    int gapDefIndex = BlockManager.GetOrCreateGapPlaceholderDefinition(
                        doc,
                        module,
                        gap.Width,
                        height,
                        depthInside,
                        depthOutside);

                    if (gapDefIndex < 0)
                        continue;

                    BlockManager.EnsureGapMaster(
                        doc,
                        module,
                        facadeBlockDefIndex,
                        gapDefIndex);

                    Point3d insertionPoint = placement.BottomLeft
                        + placement.XAxis * gap.StartOffset
                        + Vector3d.ZAxis * (span.MinZ - placement.BottomLeft.Z);

                    Transform translate = Transform.Translation(
                        insertionPoint - Point3d.Origin);

                    doc.Objects.AddInstanceObject(
                        gapDefIndex,
                        translate * orient,
                        attributes);

                    placedCount++;
                }
            }

            return placedCount;
        }
    }
}
