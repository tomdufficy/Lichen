using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Lichen.Core
{
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

        public static void PlaceFacadesOnFace(
            RhinoDoc doc,
            BrepFace face,
            int blockDefIndex,
            bool stretchHeight)
        {
            // build consistent axes from face normal and world Z
            Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
            normal.Unitize();
            if (!face.OrientationIsReversed) normal = -normal;

            // xAxis runs horizontally along the wall
            Vector3d xAxis = Vector3d.CrossProduct(Vector3d.ZAxis, normal);
            xAxis.Unitize();

            // get face world bounding box
            BoundingBox faceBBox = face.GetBoundingBox(true);

            // measure face width along xAxis
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

            // find bottom left corner in world space
            Point3d faceCentre = faceBBox.Center;
            Point3d bottomLeft = faceCentre
                + xAxis * (minX - Vector3d.Multiply(new Vector3d(faceCentre), xAxis))
                + Vector3d.ZAxis * (minZ - faceCentre.Z);

            // get block definition and measure its bounding box
            var blockDef = doc.InstanceDefinitions[blockDefIndex];
            BoundingBox moduleBBox = BoundingBox.Empty;
            foreach (var obj in blockDef.GetObjects())
                moduleBBox.Union(obj.Geometry.GetBoundingBox(true));

            if (!moduleBBox.IsValid)
            {
                RhinoApp.WriteLine("Lichen: could not get bounding box for block");
                return;
            }

            double moduleWidth = moduleBBox.Max.X - moduleBBox.Min.X;
            double moduleHeight = moduleBBox.Max.Z - moduleBBox.Min.Z;

            if (moduleWidth <= 0 || moduleHeight <= 0)
            {
                RhinoApp.WriteLine("Lichen: block has zero size");
                return;
            }

            // compute panel counts and stretch
            int countX = Math.Max(1, (int)Math.Round(faceWidth / moduleWidth));
            int countZ = Math.Max(1, (int)Math.Round(faceHeight / moduleHeight));

            double stretchX = faceWidth / (countX * moduleWidth);
            double stretchZ = stretchHeight
                ? faceHeight / (countZ * moduleHeight)
                : 1.0;

            RhinoApp.WriteLine("Lichen: placing {0}x{1} panels on face", countX, countZ);
            RhinoApp.WriteLine("  stretch {0:F3} x {1:F3}", stretchX, stretchZ);

            // build orientation transform
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

            // zero module to origin
            Transform moveToOrigin = Transform.Translation(
                -moduleBBox.Min.X, 0.0, -moduleBBox.Min.Z);

            // scale
            Transform scale = Transform.Scale(Plane.WorldXY, stretchX, 1.0, stretchZ);

            var attribs = new Rhino.DocObjects.ObjectAttributes();
            attribs.LayerIndex = BlockManager.EnsureFacadesLayer(doc);

            for (int col = 0; col < countX; col++)
            {
                for (int row = 0; row < countZ; row++)
                {
                    Point3d panelPos = bottomLeft
                        + xAxis * (col * moduleWidth * stretchX)
                        + Vector3d.ZAxis * (row * moduleHeight * stretchZ);

                    Transform translate = Transform.Translation(panelPos - Point3d.Origin);

                    Transform full = translate * orient * scale * moveToOrigin;

                    doc.Objects.AddInstanceObject(blockDefIndex, full, attribs);
                }
            }

            doc.Views.Redraw();
        }
    }
}