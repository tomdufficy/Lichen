using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Lichen.Core
{
    public class BlockManager
    {
        private const double BoxSizeMm = 10000.0;
        private const double BoxStartYMm = -25000.0;
        private const double MasterTextHeightMm = 500.0;
        private const double AdminTextHeightMm = 200.0;
        private const double LabelOffsetMm = 500.0;
        private const double LabelMarginMm = 200.0;
        private const double DottedSegmentLengthMm = 200.0;

        // Lichen palette
        private static readonly Color LichenMist = ColorTranslator.FromHtml("#F3F4F0");
        private static readonly Color LichenGreen = ColorTranslator.FromHtml("#9DB39A");
        private static readonly Color MineralBlue = ColorTranslator.FromHtml("#A9C3C9");
        private static readonly Color GoldenLichen = ColorTranslator.FromHtml("#C4B54A");
        private static readonly Color ForestSlate = ColorTranslator.FromHtml("#46534D");

        // Admin palette
        private static readonly Color AdminPink = ColorTranslator.FromHtml("#f6d9f5");

        // ─── layer setup ─────────────────────────────────────────────────────────

        private static void EnsureLayers(RhinoDoc doc, out int mastersIndex, out int adminIndex)
        {
            int lichenIndex = EnsureLayer(doc, "Lichen", LichenGreen, -1);
            mastersIndex = EnsureLayer(doc, "Masters", ForestSlate, lichenIndex);
            adminIndex = EnsureLayer(doc, "Admin", AdminPink, mastersIndex);
        }

        public static void EnsureLayers(RhinoDoc doc)
        {
            EnsureLayers(doc, out _, out _);
        }

        private static int EnsureLayer(RhinoDoc doc, string name, Color color, int parentIndex)
        {
            string fullName = parentIndex >= 0
                ? doc.Layers[parentIndex].FullPath + "::" + name
                : name;

            int found = doc.Layers.FindByFullPath(fullName, -1);
            if (found >= 0) return found;

            var layer = new Layer { Name = name, Color = color };
            if (parentIndex >= 0)
                layer.ParentLayerId = doc.Layers[parentIndex].Id;

            return doc.Layers.Add(layer);
        }

        // ─── facades layer ─────────────────────────────────────────────────────────

        public static int EnsureFacadesLayer(RhinoDoc doc)
        {
            int lichenIndex = EnsureLayer(doc, "Lichen", LichenGreen, -1);
            return EnsureLayer(doc, "Facades", LichenGreen, lichenIndex);
        }

        public static int EnsureSlabLayer(RhinoDoc doc, bool isFloor)
        {
            int lichenIndex = EnsureLayer(doc, "Lichen", LichenGreen, -1);
            int slabsIndex = EnsureLayer(doc, "Slabs", MineralBlue, lichenIndex);

            return EnsureLayer(
                doc,
                isFloor ? "Floor" : "Ceiling",
                isFloor ? MineralBlue : GoldenLichen,
                slabsIndex);
        }

        // ─── block existence check ────────────────────────────────────────────────

        public static bool BlockExists(RhinoDoc doc, string moduleName)
        {
            return doc.InstanceDefinitions.Find("Lichen::" + moduleName) != null;
        }

        // ─── block import ─────────────────────────────────────────────────────────

        public static int ImportBlock(RhinoDoc doc, FacadeModule module)
        {
            string blockName = "Lichen::" + module.Name;

            var existingDef = doc.InstanceDefinitions.Find(blockName);
            if (existingDef != null) return existingDef.Index;

            var moduleFile = Rhino.FileIO.File3dm.Read(module.FilePath);
            if (moduleFile == null)
            {
                RhinoApp.WriteLine("Lichen: could not read module file {0}", module.Name);
                return -1;
            }

            UnitSystem sourceUnits = moduleFile.Settings.ModelUnitSystem;
            UnitSystem targetUnits = doc.ModelUnitSystem;

            if (sourceUnits == UnitSystem.None)
            {
                RhinoApp.WriteLine(
                    "Lichen: facade {0} has no model units defined.",
                    module.Name);

                return -1;
            }

            double unitScale = UnitConverter.ScaleBetween(
                sourceUnits,
                targetUnits);

            Transform unitTransform = Transform.Scale(
                Point3d.Origin,
                unitScale);

            int lichenIdx = EnsureLayer(doc, "Lichen", LichenGreen, -1);

            Layer sourceLichenLayer = null;
            foreach (var candidate in moduleFile.AllLayers)
            {
                if (candidate.ParentLayerId == Guid.Empty && candidate.Name == "Lichen")
                {
                    sourceLichenLayer = candidate;
                    break;
                }
            }

            if (sourceLichenLayer == null)
            {
                RhinoApp.WriteLine(
                    "Lichen: WARNING — {0} has no top-level 'Lichen' layer. No geometry will be imported.",
                    module.Name);
            }

            var layerRemap = new Dictionary<int, int>();

            int GetOrCreateHostLayer(Layer srcLayer)
            {
                if (layerRemap.TryGetValue(srcLayer.Index, out int cached))
                    return cached;

                if (sourceLichenLayer != null && srcLayer.Index == sourceLichenLayer.Index)
                {
                    layerRemap[srcLayer.Index] = lichenIdx;
                    return lichenIdx;
                }

                if (srcLayer.ParentLayerId == Guid.Empty)
                    return -1;

                var srcParent = moduleFile.AllLayers.FindId(srcLayer.ParentLayerId);
                if (srcParent == null) return -1;

                int hostParentIdx = GetOrCreateHostLayer(srcParent);
                if (hostParentIdx < 0) return -1;

                int hostIdx = EnsureLayer(doc, srcLayer.Name, srcLayer.Color, hostParentIdx);
                layerRemap[srcLayer.Index] = hostIdx;
                return hostIdx;
            }

            foreach (var srcLayer in moduleFile.AllLayers)
                GetOrCreateHostLayer(srcLayer);

            var geometries = new List<GeometryBase>();
            var attributes = new List<ObjectAttributes>();

            foreach (var obj in moduleFile.Objects)
            {
                if (obj.Geometry == null) continue;
                if (!layerRemap.TryGetValue(obj.Attributes.LayerIndex, out int remapped)) continue;

                var attr = obj.Attributes.Duplicate();
                attr.LayerIndex = remapped;

                GeometryBase geometry = obj.Geometry.Duplicate();

                if (!geometry.Transform(unitTransform))
                {
                    RhinoApp.WriteLine(
                        "Lichen: failed to scale geometry in {0}.",
                        module.Name);

                    continue;
                }

                geometries.Add(geometry);
                attributes.Add(attr);
            }

            if (geometries.Count == 0)
            {
                RhinoApp.WriteLine("Lichen: no geometry found in {0}", module.Name);
                return -1;
            }

            int defIndex = doc.InstanceDefinitions.Add(
                blockName,
                module.Name + " facade module",
                Point3d.Origin,
                geometries,
                attributes);

            if (defIndex < 0)
            {
                RhinoApp.WriteLine("Lichen: failed to create block definition for {0}", module.Name);
                return -1;
            }

            RhinoApp.WriteLine("Lichen: imported block definition {0}", blockName);
            return defIndex;
        }

        // ─── master placement ─────────────────────────────────────────────────────

        // ─── master placement ─────────────────────────────────────────────────────

        public static void PlaceMaster(RhinoDoc doc, FacadeModule module, int blockDefIndex)
        {
            EnsureLayers(doc, out int mastersLayerIndex, out int adminLayerIndex);

            double boxSize = UnitConverter.MillimetersToModel(doc, BoxSizeMm);
            double boxStartY = UnitConverter.MillimetersToModel(doc, BoxStartYMm);
            double masterTextHeight = UnitConverter.MillimetersToModel(doc, MasterTextHeightMm);
            double adminTextHeight = UnitConverter.MillimetersToModel(doc, AdminTextHeightMm);
            double labelOffset = UnitConverter.MillimetersToModel(doc, LabelOffsetMm);
            double labelMargin = UnitConverter.MillimetersToModel(doc, LabelMarginMm);

            int masterCount = CountExistingMasters(doc, mastersLayerIndex);

            double boxMinX = 0.0;
            double boxMaxX = boxSize;
            double boxMinY = boxStartY - (masterCount * boxSize);
            double boxMaxY = boxMinY + boxSize;

            var boxPts = new Point3d[]
            {
        new Point3d(boxMinX, boxMinY, 0),
        new Point3d(boxMaxX, boxMinY, 0),
        new Point3d(boxMaxX, boxMaxY, 0),
        new Point3d(boxMinX, boxMaxY, 0),
        new Point3d(boxMinX, boxMinY, 0)
            };

            var boxCurve = new Rhino.Geometry.Polyline(boxPts).ToNurbsCurve();

            var adminAttribs = new ObjectAttributes
            {
                LayerIndex = adminLayerIndex
            };

            doc.Objects.AddCurve(boxCurve, adminAttribs);

            double facadeY = boxMinY + boxSize / 2.0;

            var facadeLine = new LineCurve(
                new Point3d(boxMinX, facadeY, 0),
                new Point3d(boxMaxX, facadeY, 0));

            var lineAttribs = new ObjectAttributes
            {
                LayerIndex = adminLayerIndex,
                LinetypeSource = ObjectLinetypeSource.LinetypeFromObject,
                LinetypeIndex = GetOrCreateDottedLinetype(doc)
            };

            doc.Objects.AddCurve(facadeLine, lineAttribs);

            AddText(
                doc,
                "outside",
                new Point3d(boxMinX + labelMargin, facadeY + labelOffset, 0),
                adminTextHeight,
                adminLayerIndex);

            AddText(
                doc,
                "inside",
                new Point3d(boxMinX + labelMargin, facadeY - labelOffset, 0),
                adminTextHeight,
                adminLayerIndex);

            AddText(
                doc,
                module.Name,
                new Point3d(boxMinX + labelMargin, boxMaxY - labelMargin, 0),
                masterTextHeight,
                adminLayerIndex);

            var blockDef = doc.InstanceDefinitions[blockDefIndex];
            var bbox = BoundingBox.Empty;

            foreach (var obj in blockDef.GetObjects())
                bbox.Union(obj.Geometry.GetBoundingBox(true));

            double moduleWidth = bbox.Max.X - bbox.Min.X;
            double offsetX = boxMinX + (boxSize - moduleWidth) / 2.0 - bbox.Min.X;
            double offsetY = facadeY;
            double offsetZ = -bbox.Min.Z;

            var masterAttribs = new ObjectAttributes
            {
                LayerIndex = mastersLayerIndex
            };

            doc.Objects.AddInstanceObject(
                blockDefIndex,
                Transform.Translation(offsetX, offsetY, offsetZ),
                masterAttribs);

            RhinoApp.WriteLine(
                "Lichen: placed master for {0}",
                module.Name);
        }

        // ─── helpers ──────────────────────────────────────────────────────────────

        private static int CountExistingMasters(RhinoDoc doc, int mastersLayerIndex)
        {
            int count = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj.Attributes.LayerIndex == mastersLayerIndex && obj is InstanceObject)
                    count++;
            }
            return count;
        }

        private static void AddText(RhinoDoc doc, string text, Point3d location,
            double height, int layerIndex)
        {
            var plane = new Plane(location, Vector3d.ZAxis);
            var textEntity = new Rhino.Geometry.TextEntity
            {
                Plane = plane,
                PlainText = text,
                TextHeight = height,
                TextVerticalAlignment = TextVerticalAlignment.Middle
            };

            doc.Objects.AddText(textEntity,
                new ObjectAttributes { LayerIndex = layerIndex });
        }

        private static int GetOrCreateDottedLinetype(RhinoDoc doc)
        {
            for (int i = 0; i < doc.Linetypes.Count; i++)
            {
                string n = doc.Linetypes[i].Name?.ToLower();
                if (n != null && (n.Contains("dot") || n.Contains("lichen")))
                    return i;
            }

            var linetype = new Linetype { Name = "Lichen_Dotted" };
            
            double segmentLength = UnitConverter.MillimetersToModel(
                doc,
                DottedSegmentLengthMm);

            linetype.AppendSegment(segmentLength, true);
            linetype.AppendSegment(segmentLength, false);

            int index = doc.Linetypes.Add(linetype);
            return index >= 0 ? index : 0;
        }
    }
}