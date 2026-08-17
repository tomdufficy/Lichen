using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Lichen.Core
{
    public enum HorizontalAlignmentMode
    {
        EvenSpacing,
        Left,
        Centre,
        Right
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

    public class FacadePlacementOptions
    {
        public bool StretchVertically { get; set; } = true;
        public bool StretchHorizontally { get; set; } = true;
        public HorizontalAlignmentMode HorizontalAlignment { get; set; } =
            HorizontalAlignmentMode.EvenSpacing;
    }

    public class FacadePlacer
    {
        private const double VerticalAngleToleranceDegrees = 15.0;

        public static List<BrepFace> GetWallFaces(Brep volume)
        {
            var wallFaces = new List<BrepFace>();

            foreach (BrepFace face in volume.Faces)
            {
                Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                normal.Unitize();

                double angleFromVertical = Vector3d.VectorAngle(normal, Vector3d.ZAxis) * (180.0 / Math.PI);

                if (angleFromVertical < VerticalAngleToleranceDegrees || angleFromVertical > 180.0 - VerticalAngleToleranceDegrees)
                {
                    continue;
                }

                wallFaces.Add(face);
            }

            return wallFaces;
        }

        public static List<FacadeVerticalSpan> PlaceFacadesOnFace(
            RhinoDoc doc,
            BrepFace face,
            int blockDefIndex,
            FacadePlacementOptions options)
        {
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

            var blockDef = doc.InstanceDefinitions[blockDefIndex];
            BoundingBox moduleBBox = BoundingBox.Empty;
            foreach (var obj in blockDef.GetObjects())
                moduleBBox.Union(obj.Geometry.GetBoundingBox(true));

            if (!moduleBBox.IsValid)
            {
                RhinoApp.WriteLine("Lichen: could not get bounding box for block");
                return new List<FacadeVerticalSpan>();
            }

            double moduleWidth = moduleBBox.Max.X - moduleBBox.Min.X;
            double moduleHeight = moduleBBox.Max.Z - moduleBBox.Min.Z;

            if (moduleWidth <= 0 || moduleHeight <= 0)
            {
                RhinoApp.WriteLine("Lichen: block has zero size");
                return new List<FacadeVerticalSpan>();
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

                switch (options.HorizontalAlignment)
                {
                    case HorizontalAlignmentMode.Left:
                        horizontalStartOffset = 0.0;
                        horizontalStep = moduleWidth;
                        break;

                    case HorizontalAlignmentMode.Centre:
                        horizontalStartOffset = remainingWidth * 0.5;
                        horizontalStep = moduleWidth;
                        break;

                    case HorizontalAlignmentMode.Right:
                        horizontalStartOffset = remainingWidth;
                        horizontalStep = moduleWidth;
                        break;

                    default:
                        double gap = remainingWidth / (countX + 1);
                        horizontalStartOffset = gap;
                        horizontalStep = moduleWidth + gap;
                        break;
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

            var attribs = new Rhino.DocObjects.ObjectAttributes();
            attribs.LayerIndex = BlockManager.EnsureFacadesLayer(doc);

            var verticalSpans = new List<FacadeVerticalSpan>();
            for (int row = 0; row < countZ; row++)
            {
                double spanMinZ = minZ + row * moduleHeight * stretchZ;
                double spanMaxZ = spanMinZ + moduleHeight * stretchZ;
                verticalSpans.Add(new FacadeVerticalSpan(spanMinZ, spanMaxZ));
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
            return verticalSpans;
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