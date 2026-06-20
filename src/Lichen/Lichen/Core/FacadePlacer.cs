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
            // get the face coordinate system
            Surface srf = face.UnderlyingSurface();
            double umid = srf.Domain(0).Mid;
            double vmid = srf.Domain(1).Mid;

            Plane facePlane;
            srf.FrameAt(umid, vmid, out facePlane);

            // get face dimensions by measuring the bounding box in face-local space
            BoundingBox faceBBox = face.GetBoundingBox(facePlane);
            double faceWidth = faceBBox.Max.X - faceBBox.Min.X;
            double faceHeight = faceBBox.Max.Y - faceBBox.Min.Y;

            // load the module file and get its bounding box
            Rhino.FileIO.File3dm moduleFile = Rhino.FileIO.File3dm.Read(module.FilePath);
            if (moduleFile == null)
            {
                RhinoApp.WriteLine("Lichen: could not read facade file {0}", module.Name);
                return;
            }

            BoundingBox moduleBBox = BoundingBox.Empty;
            foreach (var obj in moduleFile.Objects)
            {
                moduleBBox.Union(obj.Geometry.GetBoundingBox(true));
            }

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

            // compute panel counts
            int countX = (int)Math.Round(faceWidth / moduleWidth);
            int countZ = (int)Math.Round(faceHeight / moduleHeight);

            if (countX < 1) countX = 1;
            if (countZ < 1) countZ = 1;

            // compute stretch factors
            double stretchX = faceWidth / (countX * moduleWidth);
            double stretchZ = faceHeight / (countZ * moduleHeight);

            RhinoApp.WriteLine("Lichen: placing {0}x{1} panels on face", countX, countZ);
            RhinoApp.WriteLine("  stretch {0:F3} x {1:F3}", stretchX, stretchZ);

            // place panels
            for (int col = 0; col < countX; col++)
            {
                for (int row = 0; row < countZ; row++)
                {
                    double offsetX = faceBBox.Min.X + col * moduleWidth * stretchX;
                    double offsetZ = faceBBox.Min.Y + row * moduleHeight * stretchZ;

                    // build transform: scale then translate into face plane
                    Transform scale = Transform.Scale(Plane.WorldXY, stretchX, 1.0, stretchZ);

                    Transform toFacePlane = Transform.PlaneToPlane(Plane.WorldXY, facePlane);

                    Transform translate = Transform.Translation(
                        facePlane.XAxis * offsetX +
                        facePlane.YAxis * 0 +
                        facePlane.ZAxis * offsetZ);

                    Transform full = translate * toFacePlane * scale;

                    // place geometry from module file
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