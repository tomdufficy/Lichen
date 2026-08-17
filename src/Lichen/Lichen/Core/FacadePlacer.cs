using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Lichen.Core
{
    public enum HorizontalPlacementMode
    {
        Centered,
        EndToEnd
    }

    public enum CenteredEdgeGapMode
    {
        HalfInternalGap,
        EqualToInternalGap
    }

    public class FacadeVerticalSpan
    {
        public FacadeVerticalSpan(double minZ, double maxZ)
        {
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public double MinZ { get; }
        public double MaxZ { get; }
    }

    public class FacadeHorizontalGap
    {
        public FacadeHorizontalGap(double startOffset, double width, bool isEdge)
        {
            StartOffset = startOffset;
            Width = width;
            IsEdge = isEdge;
        }

        public double StartOffset { get; }
        public double Width { get; }
        public bool IsEdge { get; }
    }

    public class FacadePlacementResult
    {
        public Point3d BottomLeft { get; set; }
        public Vector3d XAxis { get; set; }
        public Vector3d Normal { get; set; }
        public List<FacadeVerticalSpan> VerticalSpans { get; } =
            new List<FacadeVerticalSpan>();
        public List<FacadeHorizontalGap> HorizontalGaps { get; } =
            new List<FacadeHorizontalGap>();
    }

    public class FacadePlacementOptions
    {
        public bool StretchVertically { get; set; } = true;
        public bool StretchHorizontally { get; set; } = true;
        public HorizontalPlacementMode HorizontalPlacement { get; set; } =
            HorizontalPlacementMode.Centered;
        public CenteredEdgeGapMode CenteredEdgeGaps { get; set; } =
            CenteredEdgeGapMode.HalfInternalGap;
    }

    public class FacadePlacer
    {
        private const double VerticalAngleToleranceDegrees = 1.0;

        public static bool HasUnsupportedCurvedFacadeFaces(
            Brep volume,
            double modelTolerance)
        {
            foreach (BrepFace face in volume.Faces)
            {
                Vector3d normal =
                    face.NormalAt(
                        face.Domain(0).Mid,
                        face.Domain(1).Mid);

                if (!normal.Unitize())
                    continue;

                double angleFromVertical =
                    Vector3d.VectorAngle(
                        normal,
                        Vector3d.ZAxis) *
                    (180.0 / Math.PI);

                bool isApproximatelyVertical =
                    angleFromVertical >= VerticalAngleToleranceDegrees &&
                    angleFromVertical <= 180.0 - VerticalAngleToleranceDegrees;

                if (isApproximatelyVertical &&
                    !face.IsPlanar(modelTolerance))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsPlanarVerticalFace(
            BrepFace face,
            double modelTolerance)
        {
            if (!face.IsPlanar(modelTolerance))
                return false;

            Vector3d normal =
                face.NormalAt(
                    face.Domain(0).Mid,
                    face.Domain(1).Mid);

            if (!normal.Unitize())
                return false;

            double angleFromVertical =
                Vector3d.VectorAngle(
                    normal,
                    Vector3d.ZAxis) *
                (180.0 / Math.PI);

            return Math.Abs(
                angleFromVertical - 90.0) <=
                VerticalAngleToleranceDegrees;
        }

        public static List<BrepFace> GetWallFaces(
            Brep volume,
            double modelTolerance)
        {
            var wallFaces = new List<BrepFace>();

            foreach (BrepFace face in volume.Faces)
            {
                if (IsPlanarVerticalFace(
                    face,
                    modelTolerance))
                {
                    wallFaces.Add(face);
                }
            }

            return wallFaces;
        }

        public static FacadePlacementResult PlaceFacadesOnFace(
            RhinoDoc doc,
            BrepFace face,
            int blockDefIndex,
            FacadePlacementOptions options)
        {
            var result = new FacadePlacementResult();

            Vector3d normal = GetFacadeNormal(face);

            Vector3d xAxis = Vector3d.CrossProduct(Vector3d.ZAxis, normal);
            xAxis.Unitize();

            BoundingBox faceBBox = face.GetBoundingBox(true);

            double minX = double.MaxValue;
            double maxX = double.MinValue;

            foreach (Point3d corner in faceBBox.GetCorners())
            {
                double proj = Vector3d.Multiply(new Vector3d(corner), xAxis);
                if (proj < minX) minX = proj;
                if (proj > maxX) maxX = proj;
            }

            double faceWidth = maxX - minX;
            double faceHeight = faceBBox.Max.Z - faceBBox.Min.Z;
            double minZ = faceBBox.Min.Z;

            Point3d faceCentre = faceBBox.Center;
            Point3d bottomLeft = faceCentre
                + xAxis * (minX - Vector3d.Multiply(new Vector3d(faceCentre), xAxis))
                + Vector3d.ZAxis * (minZ - faceCentre.Z);

            result.BottomLeft = bottomLeft;
            result.XAxis = xAxis;
            result.Normal = normal;

            var blockDef = doc.InstanceDefinitions[blockDefIndex];
            BoundingBox moduleBBox = BoundingBox.Empty;
            foreach (var obj in blockDef.GetObjects())
                moduleBBox.Union(obj.Geometry.GetBoundingBox(true));

            if (!moduleBBox.IsValid)
            {
                RhinoApp.WriteLine("Lichen: could not get bounding box for block");
                return result;
            }

            double moduleWidth = moduleBBox.Max.X - moduleBBox.Min.X;
            double moduleHeight = moduleBBox.Max.Z - moduleBBox.Min.Z;

            if (moduleWidth <= 0 || moduleHeight <= 0)
            {
                RhinoApp.WriteLine("Lichen: block has zero size");
                return result;
            }

            int countX;
            double stretchX;
            double horizontalStartOffset;
            double horizontalStep;

            if (options.StretchHorizontally)
            {
                countX = Math.Max(1, (int)Math.Round(faceWidth / moduleWidth));
                stretchX = faceWidth / (countX * moduleWidth);
                horizontalStartOffset = 0.0;
                horizontalStep = moduleWidth * stretchX;
            }
            else
            {
                countX = Math.Max(1, (int)Math.Floor(faceWidth / moduleWidth));
                stretchX = 1.0;

                double usedWidth = countX * moduleWidth;
                double remainingWidth = faceWidth - usedWidth;

                if (remainingWidth < 0.0)
                {
                    horizontalStartOffset = options.HorizontalPlacement == HorizontalPlacementMode.Centered
                        ? remainingWidth * 0.5
                        : 0.0;
                    horizontalStep = moduleWidth;
                }
                else if (options.HorizontalPlacement == HorizontalPlacementMode.EndToEnd)
                {
                    if (countX > 1)
                    {
                        double gap = remainingWidth / (countX - 1);
                        horizontalStartOffset = 0.0;
                        horizontalStep = moduleWidth + gap;
                        AddInternalGaps(result.HorizontalGaps, countX, moduleWidth, horizontalStartOffset, horizontalStep);
                    }
                    else
                    {
                        // With a single fixed-width module there is no meaningful
                        // end-to-end distribution, so centre it on the face.
                        horizontalStartOffset = remainingWidth * 0.5;
                        horizontalStep = moduleWidth;
                    }
                }
                else
                {
                    double internalGap;
                    double edgeGap;

                    if (options.CenteredEdgeGaps == CenteredEdgeGapMode.EqualToInternalGap)
                    {
                        internalGap = remainingWidth / (countX + 1);
                        edgeGap = internalGap;
                    }
                    else
                    {
                        internalGap = remainingWidth / countX;
                        edgeGap = internalGap * 0.5;
                    }

                    horizontalStartOffset = edgeGap;
                    horizontalStep = moduleWidth + internalGap;

                    AddCenteredGaps(
                        result.HorizontalGaps,
                        faceWidth,
                        countX,
                        moduleWidth,
                        horizontalStartOffset,
                        horizontalStep);
                }
            }

            int countZ;
            double stretchZ;

            if (options.StretchVertically)
            {
                countZ = Math.Max(1, (int)Math.Round(faceHeight / moduleHeight));
                stretchZ = faceHeight / (countZ * moduleHeight);
            }
            else
            {
                countZ = Math.Max(1, (int)Math.Floor(faceHeight / moduleHeight));
                stretchZ = 1.0;
            }

            RhinoApp.WriteLine("Lichen: placing {0}x{1} panels on face", countX, countZ);
            RhinoApp.WriteLine("  stretch {0:F3} x {1:F3}", stretchX, stretchZ);

            Plane sourcePlane = Plane.WorldXY;
            Vector3d tX = xAxis;
            Vector3d tY = normal;
            Vector3d tZ = Vector3d.CrossProduct(tX, tY);
            if (tZ * Vector3d.ZAxis < 0)
            {
                tZ = -tZ;
                tY = -tY;
            }
            Plane targetPlane = new Plane(Point3d.Origin, tX,
                Vector3d.CrossProduct(tZ, tX));
            Transform orient = Transform.PlaneToPlane(sourcePlane, targetPlane);

            Transform moveToOrigin = Transform.Translation(
                -moduleBBox.Min.X, 0.0, -moduleBBox.Min.Z);

            Transform scale = Transform.Scale(Plane.WorldXY, stretchX, 1.0, stretchZ);

            var attribs = new Rhino.DocObjects.ObjectAttributes
            {
                LayerIndex = BlockManager.EnsureFacadesLayer(doc)
            };

            for (int row = 0; row < countZ; row++)
            {
                double spanMinZ = minZ + row * moduleHeight * stretchZ;
                double spanMaxZ = spanMinZ + moduleHeight * stretchZ;
                result.VerticalSpans.Add(new FacadeVerticalSpan(spanMinZ, spanMaxZ));
            }

            for (int col = 0; col < countX; col++)
            {
                for (int row = 0; row < countZ; row++)
                {
                    Point3d panelPos = bottomLeft
                        + xAxis * (horizontalStartOffset + col * horizontalStep)
                        + Vector3d.ZAxis * (row * moduleHeight * stretchZ);

                    Transform translate = Transform.Translation(panelPos - Point3d.Origin);
                    Transform full = translate * orient * scale * moveToOrigin;

                    doc.Objects.AddInstanceObject(blockDefIndex, full, attribs);
                }
            }

            doc.Views.Redraw();
            return result;
        }

        private static void AddInternalGaps(
            List<FacadeHorizontalGap> gaps,
            int countX,
            double moduleWidth,
            double startOffset,
            double step)
        {
            for (int col = 0; col < countX - 1; col++)
            {
                double start = startOffset + col * step + moduleWidth;
                double width = step - moduleWidth;
                if (width > 0.0)
                    gaps.Add(new FacadeHorizontalGap(start, width, false));
            }
        }

        private static void AddCenteredGaps(
            List<FacadeHorizontalGap> gaps,
            double faceWidth,
            int countX,
            double moduleWidth,
            double startOffset,
            double step)
        {
            if (startOffset > 0.0)
                gaps.Add(new FacadeHorizontalGap(0.0, startOffset, true));

            AddInternalGaps(gaps, countX, moduleWidth, startOffset, step);

            double lastModuleEnd = startOffset + (countX - 1) * step + moduleWidth;
            double rightGap = faceWidth - lastModuleEnd;
            if (rightGap > 0.0)
                gaps.Add(new FacadeHorizontalGap(lastModuleEnd, rightGap, true));
        }

        public static Vector3d GetFacadeNormal(BrepFace face)
        {
            Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
            normal.Unitize();
            if (!face.OrientationIsReversed) normal = -normal;
            return normal;
        }
    }
}
