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

        public static void PlaceFacadesOnFace(RhinoDoc doc, BrepFace face, FacadeModule module)
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

            // load module
            Rhino.FileIO.File3dm moduleFile = Rhino.FileIO.File3dm.Read(module.FilePath);
            if (moduleFile == null)
            {
                RhinoApp.WriteLine("Lichen: could not read facade file {0}", module.Name);
                return;
            }

            BoundingBox moduleBBox = BoundingBox.Empty;
            foreach (var obj in moduleFile.Objects)
                moduleBBox.Union(obj.Geometry.GetBoundingBox(true));

            if (!moduleBBox.IsValid)
            {
                RhinoApp.WriteLine("Lichen: could not get bounding box for {0}", module.Name);
                return;
            }

            double moduleWidth = moduleBBox.Max.X - moduleBBox.Min.X;
            double moduleHeight = moduleBBox.Max.Z - moduleBBox.Min.Z;

            if (moduleWidth <= 0 || moduleHeight <= 0)
            {
                RhinoApp.WriteLine("Lichen: module {0} has zero size", module.Name);
                return;
            }

            // compute panel counts and stretch
            int countX = Math.Max(1, (int)Math.Round(faceWidth / moduleWidth));
            int countZ = Math.Max(1, (int)Math.Round(faceHeight / moduleHeight));

            double stretchX = faceWidth / (countX * moduleWidth);
            double stretchZ = faceHeight / (countZ * moduleHeight);

            RhinoApp.WriteLine("Lichen: placing {0}x{1} panels on face", countX, countZ);
            RhinoApp.WriteLine("  stretch {0:F3} x {1:F3}", stretchX, stretchZ);

            // module convention: face on XZ plane (Y=0), depth in -Y, up in +Z
            // target: xAxis horizontal, normal outward, Z up
            // we need module +Z to stay as world +Z, so we must not let
            // PlaneToPlane flip it — build the target plane with Z explicit
            Plane sourcePlane = new Plane(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis);

            // build target so X=xAxis, Y=normal, Z=ZAxis
            // use the three-vector constructor to be explicit
            Vector3d tX = xAxis;
            Vector3d tY = normal;
            Vector3d tZ = Vector3d.CrossProduct(tX, tY);
            // tZ should equal world Z for vertical walls — if it points down, flip both
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

            // module face sits at Y=0 — after orient, Y maps to normal direction
            // module depth is in -Y so it goes into the wall correctly
            // no Y offset needed since face is already at Y=0
            Transform moveToOrigin = Transform.Translation(
                -moduleBBox.Min.X, 0.0, -moduleBBox.Min.Z);

            Transform scale = Transform.Scale(Plane.WorldXY, stretchX, 1.0, stretchZ);

            for (int col = 0; col < countX; col++)
            {
                for (int row = 0; row < countZ; row++)
                {
                    Point3d panelPos = bottomLeft
                        + xAxis * (col * moduleWidth * stretchX)
                        + Vector3d.ZAxis * (row * moduleHeight * stretchZ);

                    Transform translate = Transform.Translation(panelPos - Point3d.Origin);

                    Transform full = translate * orient * scale * moveToOrigin;

                    foreach (var obj in moduleFile.Objects)
                    {
                        GeometryBase geom = obj.Geometry.Duplicate();
                        geom.Transform(full);
                        doc.Objects.Add(geom);
                    }
                }
            }

            doc.Views.Redraw();
        }
    }
}