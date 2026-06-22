using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Drawing;

namespace Lichen.Core
{
    public class BlockManager
    {
        private const string MasterLayerName = "Lichen::Masters";
        private const string AdminLayerName = "Lichen::Masters::Admin";

        private const double BoxSize = 10000.0;
        private const double BoxStartY = -25000.0;
        private const double MasterTextHeight = 500.0;
        private const double AdminTextHeight = 200.0;

        private static readonly Color LichenGreen = ColorTranslator.FromHtml("#a2b190");
        private static readonly Color LichenSlate = ColorTranslator.FromHtml("#566167");

        private static void EnsureLayers(RhinoDoc doc, out int mastersIndex, out int adminIndex)
        {
            int lichenIndex = EnsureLayer(doc, "Lichen", LichenGreen, -1);
            mastersIndex = EnsureLayer(doc, "Masters", LichenGreen, lichenIndex);
            adminIndex = EnsureLayer(doc, "Admin", LichenSlate, mastersIndex);
        }

        public static void EnsureLayers(RhinoDoc doc)
        {
            int mastersIndex, adminIndex;
            EnsureLayers(doc, out mastersIndex, out adminIndex);
        }

        private static int EnsureLayer(RhinoDoc doc, string name, Color color, int parentIndex)
        {
            string fullName = parentIndex >= 0
                ? doc.Layers[parentIndex].FullPath + "::" + name
                : name;

            Layer existing = doc.Layers.FindName(fullName);
            if (existing != null) return existing.Index;

            Layer layer = new Layer();
            layer.Name = name;
            layer.Color = color;
            if (parentIndex >= 0) layer.ParentLayerId = doc.Layers[parentIndex].Id;

            return doc.Layers.Add(layer);
        }

        public static bool BlockExists(RhinoDoc doc, string moduleName)
        {
            return doc.InstanceDefinitions.Find(moduleName) != null;
        }

        public static int ImportBlock(RhinoDoc doc, FacadeModule module)
        {
            var existingDef = doc.InstanceDefinitions.Find(module.Name);
            if (existingDef != null) return existingDef.Index;

            Rhino.FileIO.File3dm moduleFile = Rhino.FileIO.File3dm.Read(module.FilePath);
            if (moduleFile == null)
            {
                RhinoApp.WriteLine("Lichen: could not read module file {0}", module.Name);
                return -1;
            }

            var geometries = new List<GeometryBase>();
            var attributes = new List<ObjectAttributes>();

            foreach (var obj in moduleFile.Objects)
            {
                if (obj.Geometry == null) continue;
                geometries.Add(obj.Geometry.Duplicate());
                attributes.Add(obj.Attributes.Duplicate());
            }

            if (geometries.Count == 0)
            {
                RhinoApp.WriteLine("Lichen: no geometry found in {0}", module.Name);
                return -1;
            }

            int defIndex = doc.InstanceDefinitions.Add(
                module.Name,
                module.Name + " facade module",
                Point3d.Origin,
                geometries,
                attributes);

            if (defIndex < 0)
            {
                RhinoApp.WriteLine("Lichen: failed to create block definition for {0}", module.Name);
                return -1;
            }

            RhinoApp.WriteLine("Lichen: imported block definition {0}", module.Name);
            return defIndex;
        }

        public static void PlaceMaster(RhinoDoc doc, FacadeModule module, int blockDefIndex)
        {
            int mastersLayerIndex, adminLayerIndex;
            EnsureLayers(doc, out mastersLayerIndex, out adminLayerIndex);

            int masterCount = CountExistingMasters(doc);

            double boxMinX = masterCount * BoxSize;
            double boxMaxX = boxMinX + BoxSize;
            double boxMinY = BoxStartY;
            double boxMaxY = BoxStartY + BoxSize;

            // bounding box rectangle
            Point3d boxBL = new Point3d(boxMinX, boxMinY, 0);
            Point3d boxBR = new Point3d(boxMaxX, boxMinY, 0);
            Point3d boxTR = new Point3d(boxMaxX, boxMaxY, 0);
            Point3d boxTL = new Point3d(boxMinX, boxMaxY, 0);

            var boxPts = new Point3d[] { boxBL, boxBR, boxTR, boxTL, boxBL };
            var boxCurve = new Rhino.Geometry.Polyline(boxPts).ToNurbsCurve();

            var adminAttribs = new ObjectAttributes();
            adminAttribs.LayerIndex = adminLayerIndex;
            doc.Objects.AddCurve(boxCurve, adminAttribs);

            // dotted line at facade face (Y = box centre)
            double facadeY = BoxStartY + BoxSize / 2.0;
            Point3d lineStart = new Point3d(boxMinX, facadeY, 0);
            Point3d lineEnd = new Point3d(boxMaxX, facadeY, 0);
            var facadeLine = new LineCurve(lineStart, lineEnd);

            var lineAttribs = new ObjectAttributes();
            lineAttribs.LayerIndex = adminLayerIndex;
            lineAttribs.LinetypeSource = ObjectLinetypeSource.LinetypeFromObject;
            lineAttribs.LinetypeIndex = GetOrCreateDottedLinetype(doc);
            doc.Objects.AddCurve(facadeLine, lineAttribs);

            double labelOffset = 500.0;

            // outside text — 500mm above dotted line, vertically centred
            AddText(doc, "outside",
                new Point3d(boxMinX + 200, facadeY + labelOffset, 0),
                AdminTextHeight, adminLayerIndex);

            // inside text — 500mm below dotted line, vertically centred
            AddText(doc, "inside",
                new Point3d(boxMinX + 200, facadeY - labelOffset, 0),
                AdminTextHeight, adminLayerIndex);

            // module name — top left of box
            AddText(doc, module.Name,
                new Point3d(boxMinX + 200, boxMaxY - 200, 0),
                MasterTextHeight, adminLayerIndex);

            // place master block standing up, face at facade line, centred in X
            var blockDef = doc.InstanceDefinitions[blockDefIndex];
            BoundingBox bbox = BoundingBox.Empty;
            foreach (var obj in blockDef.GetObjects())
                bbox.Union(obj.Geometry.GetBoundingBox(true));

            double moduleWidth = bbox.Max.X - bbox.Min.X;

            double offsetX = boxMinX + (BoxSize - moduleWidth) / 2.0 - bbox.Min.X;
            double offsetY = facadeY - bbox.Min.Y;
            double offsetZ = -bbox.Min.Z;

            Transform masterTransform = Transform.Translation(offsetX, offsetY, offsetZ);

            var masterAttribs = new ObjectAttributes();
            masterAttribs.LayerIndex = mastersLayerIndex;
            doc.Objects.AddInstanceObject(blockDefIndex, masterTransform, masterAttribs);

            RhinoApp.WriteLine("Lichen: placed master for {0}", module.Name);
        }

        private static int CountExistingMasters(RhinoDoc doc)
        {
            var mastersLayer = doc.Layers.FindName(MasterLayerName);
            if (mastersLayer == null) return 0;

            int count = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj.Attributes.LayerIndex == mastersLayer.Index
                    && obj is InstanceObject)
                    count++;
            }
            return count;
        }

        private static void AddText(RhinoDoc doc, string text, Point3d location,
            double height, int layerIndex)
        {
            var plane = new Plane(location, Vector3d.ZAxis);
            var textEntity = new Rhino.Geometry.TextEntity();
            textEntity.Plane = plane;
            textEntity.PlainText = text;
            textEntity.TextHeight = height;
            textEntity.TextVerticalAlignment = TextVerticalAlignment.Middle;

            var attribs = new ObjectAttributes();
            attribs.LayerIndex = layerIndex;
            doc.Objects.AddText(textEntity, attribs);
        }

        private static int GetOrCreateDottedLinetype(RhinoDoc doc)
        {
            for (int i = 0; i < doc.Linetypes.Count; i++)
            {
                if (doc.Linetypes[i].Name.ToLower().Contains("dot")
                    || doc.Linetypes[i].Name.ToLower().Contains("lichen"))
                    return i;
            }

            var linetype = new Linetype();
            linetype.Name = "Lichen_Dotted";
            linetype.AppendSegment(200.0, true);
            linetype.AppendSegment(200.0, false);

            int index = doc.Linetypes.Add(linetype);
            return index >= 0 ? index : 0;
        }
    }
}