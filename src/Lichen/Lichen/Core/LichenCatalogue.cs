using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lichen.Core
{
    public class FacadeModule
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
    }

    public class LichenCatalogue
    {
        private readonly string _facadesFolder;
        private List<FacadeModule> _modules = new List<FacadeModule>();

        // Spacing between master preview blocks in the Lichen::Masters layer (world units)
        private const double MasterSpacing = 1.0;

        public LichenCatalogue(string facadesFolder)
        {
            _facadesFolder = facadesFolder;
        }

        public IReadOnlyList<FacadeModule> Modules => _modules.AsReadOnly();

        // ─── catalogue discovery ────────────────────────────────────────────────

        public void Load()
        {
            _modules.Clear();

            if (!Directory.Exists(_facadesFolder))
            {
                RhinoApp.WriteLine("Lichen: facades folder not found at {0}", _facadesFolder);
                return;
            }

            foreach (var file in Directory.GetFiles(_facadesFolder, "*.3dm"))
            {
                _modules.Add(new FacadeModule
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
            }

            RhinoApp.WriteLine("Lichen: loaded {0} facade module(s).", _modules.Count);
        }

        // ─── block import ───────────────────────────────────────────────────────

        /// <summary>
        /// Imports a facade .3dm into <paramref name="doc"/> as a block definition,
        /// preserving every source layer and placing a master instance on the
        /// hidden Lichen::Masters layer.
        ///
        /// Returns the index of the block definition in doc.InstanceDefinitions,
        /// or -1 on failure.
        /// </summary>
        public static int ImportFacadeAsBlock(RhinoDoc doc, FacadeModule module)
        {
            // ── 1. already imported? return existing def ──────────────────────
            string blockName = "Lichen::" + module.Name;
            var existing = doc.InstanceDefinitions.Find(blockName);
            if (existing != null)
                return existing.Index;

            // ── 2. open source file ───────────────────────────────────────────
            var sourceFile = File3dm.Read(module.FilePath);
            if (sourceFile == null)
            {
                RhinoApp.WriteLine("Lichen: could not read {0}", module.FilePath);
                return -1;
            }

            // ── 3. merge source layers into host doc, build index remap ───────
            //
            //  For each layer in the source file we find-or-create a matching
            //  layer in the host doc under a "Lichen::Facades::<ModuleName>"
            //  parent, then record  sourceLayerIndex → hostLayerIndex.
            //
            var layerRemap = new Dictionary<int, int>();

            // Ensure the parent layer exists
            string parentLayerName = "Lichen::Facades::" + module.Name;
            int parentLayerIndex = FindOrCreateLayer(doc, parentLayerName, hidden: false);

            // Walk source layers in index order so parent layers are always
            // created before their children.
            var sourceLayers = sourceFile.AllLayers;
            foreach (var srcLayer in sourceLayers)
            {
                // Build a qualified host-layer name
                string hostLayerName = srcLayer.ParentLayerId == Guid.Empty
                    ? parentLayerName + "::" + srcLayer.Name
                    : parentLayerName + "::" + srcLayer.FullPath; // FullPath uses :: separators

                int hostIndex = FindOrCreateLayer(doc, hostLayerName, hidden: false);
                layerRemap[srcLayer.Index] = hostIndex;
            }

            // ── 4. collect geometry objects, remap layer indices ──────────────
            var defObjects = new List<GeometryBase>();
            var defAttribs = new List<ObjectAttributes>();

            foreach (var srcObj in sourceFile.Objects)
            {
                GeometryBase geom = srcObj.Geometry;
                if (geom == null) continue;

                var attr = srcObj.Attributes.Duplicate();

                // Remap layer
                if (layerRemap.TryGetValue(attr.LayerIndex, out int remapped))
                    attr.LayerIndex = remapped;
                else
                    attr.LayerIndex = parentLayerIndex; // fallback: parent layer

                defObjects.Add(geom);
                defAttribs.Add(attr);
            }

            if (defObjects.Count == 0)
            {
                RhinoApp.WriteLine("Lichen: no geometry found in {0}", module.Name);
                return -1;
            }

            // ── 5. create block definition ────────────────────────────────────
            int blockDefIndex = doc.InstanceDefinitions.Add(
                blockName,
                module.Name + " facade module",
                Point3d.Origin,
                defObjects,
                defAttribs);

            if (blockDefIndex < 0)
            {
                RhinoApp.WriteLine("Lichen: failed to create block definition for {0}", module.Name);
                return -1;
            }

            // ── 6. place master instance on Lichen::Masters ───────────────────
            PlaceMasterInstance(doc, blockDefIndex);

            return blockDefIndex;
        }

        // ─── master instance placement ──────────────────────────────────────────

        /// <summary>
        /// Inserts one block reference on the hidden Lichen::Masters layer.
        /// Each new master is offset along +Y by (accumulatedBBoxDepth + spacing)
        /// so they never stack on top of each other.
        /// </summary>
        private static void PlaceMasterInstance(RhinoDoc doc, int blockDefIndex)
        {
            int mastersLayerIndex = FindOrCreateLayer(doc, "Lichen::Masters", hidden: true);

            // ── measure all existing masters to find next Y offset ─────────────
            //
            //  We iterate every object already on Lichen::Masters and grow a
            //  bounding box, then place the new master just past its Max.Y.
            //
            double nextY = 0.0;

            var mastersLayer = doc.Layers[mastersLayerIndex];
            var existingObjs = doc.Objects.FindByLayer(mastersLayer);

            if (existingObjs != null && existingObjs.Length > 0)
            {
                BoundingBox occupied = BoundingBox.Empty;
                foreach (var obj in existingObjs)
                {
                    BoundingBox bb = obj.Geometry.GetBoundingBox(true);
                    if (bb.IsValid)
                        occupied.Union(bb);
                }

                if (occupied.IsValid)
                    nextY = occupied.Max.Y + MasterSpacing;
            }

            // ── place at (0, nextY, 0) ─────────────────────────────────────────
            var masterAttribs = new ObjectAttributes
            {
                LayerIndex = mastersLayerIndex,
                Name = doc.InstanceDefinitions[blockDefIndex].Name + "_master"
            };

            Transform placement = Transform.Translation(0.0, nextY, 0.0);
            doc.Objects.AddInstanceObject(blockDefIndex, placement, masterAttribs);

            // Keep the masters layer hidden
            doc.Layers[mastersLayerIndex].IsVisible = false;
            doc.Views.Redraw();
        }

        // ─── layer helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Finds a layer by its full path (using "::" as separator) or creates it,
        /// including any missing parent layers. Returns the layer index.
        /// </summary>
        private static int FindOrCreateLayer(RhinoDoc doc, string fullPath, bool hidden)
        {
            // Rhino stores layer full paths with "::" separators internally
            var existing = doc.Layers.FindByFullPath(fullPath, RhinoMath.UnsetIntIndex);
            if (existing >= 0)
                return existing;

            // Split and ensure each ancestor exists
            string[] parts = fullPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            int parentIdx = -1;

            string accumulated = string.Empty;
            foreach (string part in parts)
            {
                accumulated = accumulated.Length == 0 ? part : accumulated + "::" + part;

                int found = doc.Layers.FindByFullPath(accumulated, RhinoMath.UnsetIntIndex);
                if (found >= 0)
                {
                    parentIdx = found;
                    continue;
                }

                // Create this layer
                var newLayer = new Layer { Name = part };
                if (parentIdx >= 0)
                    newLayer.ParentLayerId = doc.Layers[parentIdx].Id;

                newLayer.IsVisible = !hidden;

                parentIdx = doc.Layers.Add(newLayer);
            }

            return parentIdx;
        }
    }
}