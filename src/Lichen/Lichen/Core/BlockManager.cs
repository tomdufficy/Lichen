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
        private const double BoxSize = 10000.0;
        private const double BoxStartY = -25000.0;
        private const double MasterTextHeight = 500.0;
        private const double AdminTextHeight = 200.0;

        private static readonly Color LichenGreen = ColorTranslator.FromHtml("#a2b190");
        private static readonly Color LichenSlate = ColorTranslator.FromHtml("#566167");

        // ─── layer setup ─────────────────────────────────────────────────────────

        private static void EnsureLayers(RhinoDoc doc, out int mastersIndex, out int adminIndex)
        {
            int lichenIndex = EnsureLayer(doc, "Lichen", LichenGreen, -1);
            mastersIndex = EnsureLayer(doc, "Masters", LichenGreen, lichenIndex);
            adminIndex = EnsureLayer(doc, "Admin", LichenSlate, mastersIndex);
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

            // ── build facade layer hierarchy in host doc ──────────────────────────
            int lichenIdx = EnsureLayer(doc, "Lichen", LichenGreen, -1);

            // ── find the source file's own top-level "Lichen" layer ───────────────
            // This is the anchor: it gets stripped, and everything beneath it is
            // grafted directly onto the host's Lichen layer. Anything OUTSIDE this
            // subtree (e.g. a "Guides" layer sitting alongside it) is excluded
            // entirely — no layer created, no geometry imported, no effect on the
            // block's bounding box.
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

            // ── remap source layers, merging same-named layers across modules ─────
            // Walk each source layer up to its real parent (by Id, not by parsing
            // FullPath). Layers under the source "Lichen" layer are recreated under
            // the host's Lichen layer using only their short Name, so identically
            // named/nested layers from different modules land on the same host
            // layer — EnsureLayer's find-by-full-path check does the merging.
            // Recursive + memoized via layerRemap so parents always exist before
            // children. Anything not reachable from sourceLichenLayer is left out
            // of layerRemap entirely, which excludes it downstream.
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
                    return -1; // top-level layer other than "Lichen" (e.g. Guides) — excluded

                var srcParent = moduleFile.AllLayers.FindId(srcLayer.ParentLayerId);
                if (srcParent == null) return -1;

                int hostParentIdx = GetOrCreateHostLayer(srcParent);
                if (hostParentIdx < 0) return -1; // parent excluded — this is excluded too

                int hostIdx = EnsureLayer(doc, srcLayer.Name, srcLayer.Color, hostParentIdx);
                layerRemap[srcLayer.Index] = hostIdx;
                return hostIdx;
            }

            foreach (var srcLayer in moduleFile.AllLayers)
                GetOrCreateHostLayer(srcLayer);

            // ── collect geometry ──────────────────────────────────────────────────
            // Objects whose layer isn't in layerRemap (Guides, or anything else
            // outside the source Lichen subtree) are skipped entirely — they never
            // become part of the block, so they never affect its bounding box,
            // orientation, or size.
            var geometries = new List<GeometryBase>();
            var attributes = new List<ObjectAttributes>();

            foreach (var obj in moduleFile.Objects)
            {
                if (obj.Geometry == null) continue;
                if (!layerRemap.TryGetValue(obj.Attributes.LayerIndex, out int remapped)) continue;

                var attr = obj.Attributes.Duplicate();
                attr.LayerIndex = remapped;

                geometries.Add(obj.Geometry.Duplicate());
                attributes.Add(attr);
            }

            if (geometries.Count == 0)
            {
                RhinoApp.WriteLine("Lichen: no geometry found in {0}", module.Name);
                return -1;
            }

            // ── create block definition ───────────────────────────────────────────
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

        public static void PlaceMaster(RhinoDoc doc, FacadeModule module, int blockDefIndex)
        {
            EnsureLayers(doc, out int mastersLayerIndex, out int adminLayerIndex);

            int masterCount = CountExistingMasters(doc, mastersLayerIndex);

            double boxMinX = 0;
            double boxMaxX = BoxSize;
            double boxMinY = BoxStartY - (masterCount * BoxSize);
            double boxMaxY = boxMinY + BoxSize;

            // ── bounding box rectangle ────────────────────────────────────────────
            var boxPts = new Point3d[]
            {
                new Point3d(boxMinX, boxMinY, 0),
                new Point3d(boxMaxX, boxMinY, 0),
                new Point3d(boxMaxX, boxMaxY, 0),
                new Point3d(boxMinX, boxMaxY, 0),
                new Point3d(boxMinX, boxMinY, 0)
            };
            var boxCurve = new Rhino.Geometry.Polyline(boxPts).ToNurbsCurve();

            var adminAttribs = new ObjectAttributes { LayerIndex = adminLayerIndex };
            doc.Objects.AddCurve(boxCurve, adminAttribs);

            // ── facade face line (dotted) ─────────────────────────────────────────
            double facadeY = boxMinY + BoxSize / 2.0;
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

            // ── labels ────────────────────────────────────────────────────────────
            double labelOffset = 500.0;

            AddText(doc, "outside",
                new Point3d(boxMinX + 200, facadeY + labelOffset, 0),
                AdminTextHeight, adminLayerIndex);

            AddText(doc, "inside",
                new Point3d(boxMinX + 200, facadeY - labelOffset, 0),
                AdminTextHeight, adminLayerIndex);

            AddText(doc, module.Name,
                new Point3d(boxMinX + 200, boxMaxY - 200, 0),
                MasterTextHeight, adminLayerIndex);

            // ── master block instance ─────────────────────────────────────────────
            var blockDef = doc.InstanceDefinitions[blockDefIndex];
            var bbox = BoundingBox.Empty;
            foreach (var obj in blockDef.GetObjects())
                bbox.Union(obj.Geometry.GetBoundingBox(true));

            double moduleWidth = bbox.Max.X - bbox.Min.X;
            double offsetX = boxMinX + (BoxSize - moduleWidth) / 2.0 - bbox.Min.X;
            double offsetY = facadeY;
            double offsetZ = -bbox.Min.Z;

            var masterAttribs = new ObjectAttributes { LayerIndex = mastersLayerIndex };
            doc.Objects.AddInstanceObject(
                blockDefIndex,
                Transform.Translation(offsetX, offsetY, offsetZ),
                masterAttribs);

            RhinoApp.WriteLine("Lichen: placed master for {0}", module.Name);
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
            linetype.AppendSegment(200.0, true);
            linetype.AppendSegment(200.0, false);

            int index = doc.Linetypes.Add(linetype);
            return index >= 0 ? index : 0;
        }
    }
}