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
        private const double MasterTextHeightMm = 250.0;
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

        public static int EnsureFacadeHelplinesLayer(RhinoDoc doc)
        {
            return EnsureLayer(doc, "Helplines", AdminPink, EnsureFacadesLayer(doc));
        }

        public static int EnsureCornersLayer(RhinoDoc doc)
        {
            int lichenIndex = EnsureLayer(doc, "Lichen", LichenGreen, -1);
            return EnsureLayer(doc, "Corners", GoldenLichen, lichenIndex);
        }

        public static int EnsureCornerHelplinesLayer(RhinoDoc doc)
        {
            return EnsureLayer(doc, "Helplines", AdminPink, EnsureCornersLayer(doc));
        }

        public static int EnsureGapsLayer(RhinoDoc doc)
        {
            int lichenIndex = EnsureLayer(doc, "Lichen", LichenGreen, -1);
            return EnsureLayer(doc, "Gaps", MineralBlue, lichenIndex);
        }

        public static int EnsureGapHelplinesLayer(RhinoDoc doc)
        {
            return EnsureLayer(doc, "Helplines", AdminPink, EnsureGapsLayer(doc));
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


        public static int GetOrCreateCustomFacadeDefinition(
            RhinoDoc doc,
            string blockName,
            double widthMm,
            double heightMm,
            double depthInsideMm,
            double depthOutsideMm,
            out bool wasCreated)
        {
            wasCreated = false;

            var existing = doc.InstanceDefinitions.Find(blockName);
            if (existing != null)
                return existing.Index;

            double width = UnitConverter.MillimetersToModel(doc, widthMm);
            double height = UnitConverter.MillimetersToModel(doc, heightMm);
            double depthInside = UnitConverter.MillimetersToModel(doc, depthInsideMm);
            double depthOutside = UnitConverter.MillimetersToModel(doc, depthOutsideMm);

            int layerIndex = EnsureFacadeHelplinesLayer(doc);
            var geometries = CreateWireframeBox(
                width,
                -depthInside,
                depthOutside,
                height);
            var attributes = new List<ObjectAttributes>();
            foreach (var geometry in geometries)
                attributes.Add(CreatePlaceholderAttributes(layerIndex));

            string description = FormatCustomFacadeDescription(
                widthMm,
                heightMm,
                depthInsideMm,
                depthOutsideMm);

            int definitionIndex = doc.InstanceDefinitions.Add(
                blockName,
                description,
                Point3d.Origin,
                geometries,
                attributes);

            if (definitionIndex >= 0)
            {
                wasCreated = true;
                RhinoApp.WriteLine("Lichen: created custom facade definition {0}", blockName);
            }

            return definitionIndex;
        }

        public static bool CustomFacadeDimensionsMatch(
            RhinoDoc doc,
            string blockName,
            double widthMm,
            double heightMm,
            double depthInsideMm,
            double depthOutsideMm)
        {
            var definition = doc.InstanceDefinitions.Find(blockName);
            if (definition == null)
                return true;

            if (TryParseCustomFacadeDescription(
                definition.Description,
                out double storedWidth,
                out double storedHeight,
                out double storedInside,
                out double storedOutside))
            {
                return NearlyEqual(storedWidth, widthMm) &&
                       NearlyEqual(storedHeight, heightMm) &&
                       NearlyEqual(storedInside, depthInsideMm) &&
                       NearlyEqual(storedOutside, depthOutsideMm);
            }

            BoundingBox bbox = BoundingBox.Empty;
            foreach (var obj in definition.GetObjects())
                bbox.Union(obj.Geometry.GetBoundingBox(true));

            if (!bbox.IsValid)
                return false;

            double mmPerModelUnit = 1.0 / UnitConverter.MillimetersToModel(doc, 1.0);
            double actualWidth = (bbox.Max.X - bbox.Min.X) * mmPerModelUnit;
            double actualHeight = (bbox.Max.Z - bbox.Min.Z) * mmPerModelUnit;
            double actualInside = Math.Max(0.0, -bbox.Min.Y) * mmPerModelUnit;
            double actualOutside = Math.Max(0.0, bbox.Max.Y) * mmPerModelUnit;

            return NearlyEqual(actualWidth, widthMm) &&
                   NearlyEqual(actualHeight, heightMm) &&
                   NearlyEqual(actualInside, depthInsideMm) &&
                   NearlyEqual(actualOutside, depthOutsideMm);
        }

        public static void GetFacadeDepths(
            RhinoDoc doc,
            int blockDefIndex,
            out double depthInside,
            out double depthOutside)
        {
            depthInside = 0.0;
            depthOutside = 0.0;

            var definition = doc.InstanceDefinitions[blockDefIndex];
            if (definition == null)
                return;

            BoundingBox bbox = BoundingBox.Empty;
            foreach (var obj in definition.GetObjects())
                bbox.Union(obj.Geometry.GetBoundingBox(true));

            if (!bbox.IsValid)
                return;

            depthInside = Math.Max(0.0, -bbox.Min.Y);
            depthOutside = Math.Max(0.0, bbox.Max.Y);
        }

        private static string FormatCustomFacadeDescription(
            double widthMm,
            double heightMm,
            double depthInsideMm,
            double depthOutsideMm)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Lichen custom facade; WidthMm={0:R}; HeightMm={1:R}; DepthInsideMm={2:R}; DepthOutsideMm={3:R}",
                widthMm,
                heightMm,
                depthInsideMm,
                depthOutsideMm);
        }

        private static bool TryParseCustomFacadeDescription(
            string description,
            out double widthMm,
            out double heightMm,
            out double depthInsideMm,
            out double depthOutsideMm)
        {
            widthMm = heightMm = depthInsideMm = depthOutsideMm = 0.0;
            if (string.IsNullOrWhiteSpace(description) ||
                !description.StartsWith("Lichen custom facade;", StringComparison.Ordinal))
                return false;

            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            string[] parts = description.Split(';');
            foreach (string part in parts)
            {
                int equals = part.IndexOf('=');
                if (equals < 0)
                    continue;

                string key = part.Substring(0, equals).Trim();
                string value = part.Substring(equals + 1).Trim();
                if (double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double parsed))
                {
                    values[key] = parsed;
                }
            }

            return values.TryGetValue("WidthMm", out widthMm) &&
                   values.TryGetValue("HeightMm", out heightMm) &&
                   values.TryGetValue("DepthInsideMm", out depthInsideMm) &&
                   values.TryGetValue("DepthOutsideMm", out depthOutsideMm);
        }

        private static bool NearlyEqual(double a, double b)
        {
            return Math.Abs(a - b) <= 0.01;
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

            int masterCount = CountExistingFacadeMasters(doc, mastersLayerIndex);

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

        // ─── corner placeholder blocks ─────────────────────────────────────────────

        public static int GetOrCreateCornerPlaceholderDefinition(
            RhinoDoc doc,
            FacadeModule module,
            double angleDegrees,
            bool isConcave,
            double height,
            double legLengthMm,
            double offsetMm)
        {
            double roundedAngle = Math.Round(angleDegrees / 0.1) * 0.1;
            double heightMm = height / UnitConverter.MillimetersToModel(doc, 1.0);
            double roundedHeightMm = Math.Round(heightMm);

            string angleText = Math.Abs(roundedAngle - Math.Round(roundedAngle)) < 1e-9
                ? Math.Round(roundedAngle).ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                : roundedAngle.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

            string heightText = roundedHeightMm.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            string blockName = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Lichen::CornerPlaceholder::{0}::{1}_{2}_H{3}",
                module.Name,
                isConcave ? "Concave" : "Convex",
                angleText,
                heightText);

            var existing = doc.InstanceDefinitions.Find(blockName);
            if (existing != null)
                return existing.Index;

            double legLength = UnitConverter.MillimetersToModel(doc, legLengthMm);
            double offset = UnitConverter.MillimetersToModel(doc, offsetMm);
            double theta = roundedAngle * Math.PI / 180.0;

            Vector3d d1 = Vector3d.XAxis;
            Vector3d d2 = new Vector3d(Math.Cos(theta), Math.Sin(theta), 0.0);

            Vector3d n1;
            Vector3d n2;
            if (isConcave)
            {
                n1 = new Vector3d(0.0, 1.0, 0.0);
                n2 = new Vector3d(Math.Sin(theta), -Math.Cos(theta), 0.0);
            }
            else
            {
                n1 = new Vector3d(0.0, -1.0, 0.0);
                n2 = new Vector3d(-Math.Sin(theta), Math.Cos(theta), 0.0);
            }

            Point3d corner = Point3d.Origin;
            Point3d p1 = corner + d1 * legLength;
            Point3d p2 = corner + d2 * legLength;
            Point3d q1 = p1 + n1 * offset;
            Point3d q2 = p2 + n2 * offset;

            if (!TryIntersectPlanLines(q1, d1, q2, d2, out Point3d outerCorner))
            {
                RhinoApp.WriteLine("Lichen: could not construct corner placeholder for {0}", blockName);
                return -1;
            }

            var bottom = new[] { corner, p1, q1, outerCorner, q2, p2 };
            var geometries = new List<GeometryBase>();
            int cornersLayerIndex = EnsureCornerHelplinesLayer(doc);
            var attributes = new List<ObjectAttributes>();

            void AddSegment(Point3d a, Point3d b)
            {
                geometries.Add(new LineCurve(a, b));
                attributes.Add(CreatePlaceholderAttributes(cornersLayerIndex));
            }

            for (int i = 0; i < bottom.Length; i++)
            {
                int next = (i + 1) % bottom.Length;
                AddSegment(bottom[i], bottom[next]);
            }

            var top = new Point3d[bottom.Length];
            for (int i = 0; i < bottom.Length; i++)
            {
                top[i] = bottom[i] + Vector3d.ZAxis * height;
            }

            for (int i = 0; i < top.Length; i++)
            {
                int next = (i + 1) % top.Length;
                AddSegment(top[i], top[next]);
                AddSegment(bottom[i], top[i]);
            }

            int definitionIndex = doc.InstanceDefinitions.Add(
                blockName,
                module.Name + " corner placeholder",
                Point3d.Origin,
                geometries,
                attributes);

            if (definitionIndex >= 0)
            {
                RhinoApp.WriteLine("Lichen: created corner placeholder definition {0}", blockName);
            }

            return definitionIndex;
        }

        public static void EnsureCornerMaster(
            RhinoDoc doc,
            FacadeModule module,
            int facadeBlockDefIndex,
            int cornerBlockDefIndex)
        {
            EnsureAssociatedMaster(
                doc,
                module,
                facadeBlockDefIndex,
                cornerBlockDefIndex,
                "corner");
        }

        private static void EnsureAssociatedMaster(
            RhinoDoc doc,
            FacadeModule module,
            int facadeBlockDefIndex,
            int associatedBlockDefIndex,
            string kind)
        {
            EnsureLayers(doc, out int mastersLayerIndex, out int adminLayerIndex);

            foreach (var obj in doc.Objects)
            {
                if (obj.Attributes.LayerIndex == mastersLayerIndex &&
                    obj is InstanceObject instance &&
                    instance.InstanceDefinition != null &&
                    instance.InstanceDefinition.Index == associatedBlockDefIndex)
                {
                    return;
                }
            }

            int facadeRow = FindFacadeMasterRow(doc, mastersLayerIndex, facadeBlockDefIndex);
            if (facadeRow < 0)
                facadeRow = Math.Max(0, CountExistingFacadeMasters(doc, mastersLayerIndex) - 1);

            int column = 1 + CountAssociatedMastersForFacade(doc, mastersLayerIndex, module.Name);

            double boxSize = UnitConverter.MillimetersToModel(doc, BoxSizeMm);
            double boxStartY = UnitConverter.MillimetersToModel(doc, BoxStartYMm);
            double masterTextHeight = UnitConverter.MillimetersToModel(doc, MasterTextHeightMm);
            double adminTextHeight = UnitConverter.MillimetersToModel(doc, AdminTextHeightMm);
            double labelOffset = UnitConverter.MillimetersToModel(doc, LabelOffsetMm);
            double labelMargin = UnitConverter.MillimetersToModel(doc, LabelMarginMm);

            double boxMinX = column * boxSize;
            double boxMaxX = boxMinX + boxSize;
            double boxMinY = boxStartY - facadeRow * boxSize;
            double boxMaxY = boxMinY + boxSize;

            var adminAttribs = new ObjectAttributes { LayerIndex = adminLayerIndex };
            var boxPts = new[]
            {
                new Point3d(boxMinX, boxMinY, 0.0),
                new Point3d(boxMaxX, boxMinY, 0.0),
                new Point3d(boxMaxX, boxMaxY, 0.0),
                new Point3d(boxMinX, boxMaxY, 0.0),
                new Point3d(boxMinX, boxMinY, 0.0)
            };
            doc.Objects.AddCurve(new Polyline(boxPts).ToNurbsCurve(), adminAttribs);

            var definition = doc.InstanceDefinitions[associatedBlockDefIndex];
            string label = definition?.Name ?? (kind + " placeholder");
            int separator = label.LastIndexOf("::", StringComparison.Ordinal);
            if (separator >= 0 && separator + 2 < label.Length)
                label = label.Substring(separator + 2);

            AddText(
                doc,
                label,
                new Point3d(boxMinX + labelMargin, boxMaxY - labelMargin, 0.0),
                masterTextHeight,
                adminLayerIndex);

            BoundingBox bbox = BoundingBox.Empty;
            if (definition != null)
            {
                foreach (var obj in definition.GetObjects())
                    bbox.Union(obj.Geometry.GetBoundingBox(true));
            }

            if (!bbox.IsValid)
                return;

            double offsetX = boxMinX + (boxSize - (bbox.Max.X - bbox.Min.X)) * 0.5 - bbox.Min.X;
            double offsetY = boxMinY + (boxSize - (bbox.Max.Y - bbox.Min.Y)) * 0.5 - bbox.Min.Y;
            double offsetZ = -bbox.Min.Z;

            if (string.Equals(kind, "gap", StringComparison.Ordinal))
            {
                AddGapMasterDatum(
                    doc,
                    boxMinX,
                    boxMaxX,
                    offsetY,
                    labelMargin,
                    labelOffset,
                    adminTextHeight,
                    adminLayerIndex);
            }
            else if (string.Equals(kind, "corner", StringComparison.Ordinal))
            {
                AddCornerMasterDatum(
                    doc,
                    definition?.Name,
                    new Point3d(offsetX, offsetY, 0.0),
                    boxMinX,
                    boxMaxX,
                    boxMinY,
                    boxMaxY,
                    labelOffset,
                    adminTextHeight,
                    adminLayerIndex);
            }

            doc.Objects.AddInstanceObject(
                associatedBlockDefIndex,
                Transform.Translation(offsetX, offsetY, offsetZ),
                new ObjectAttributes { LayerIndex = mastersLayerIndex });

            RhinoApp.WriteLine("Lichen: placed {0} master for {1}", kind, label);
        }

        private static void AddGapMasterDatum(
            RhinoDoc doc,
            double boxMinX,
            double boxMaxX,
            double datumY,
            double labelMargin,
            double labelOffset,
            double adminTextHeight,
            int adminLayerIndex)
        {
            var lineAttribs = CreateDottedAdminAttributes(doc, adminLayerIndex);
            doc.Objects.AddCurve(
                new LineCurve(
                    new Point3d(boxMinX, datumY, 0.0),
                    new Point3d(boxMaxX, datumY, 0.0)),
                lineAttribs);

            AddText(
                doc,
                "outside",
                new Point3d(boxMinX + labelMargin, datumY + labelOffset, 0.0),
                adminTextHeight,
                adminLayerIndex);

            AddText(
                doc,
                "inside",
                new Point3d(boxMinX + labelMargin, datumY - labelOffset, 0.0),
                adminTextHeight,
                adminLayerIndex);
        }

        private static void AddCornerMasterDatum(
            RhinoDoc doc,
            string definitionName,
            Point3d vertex,
            double boxMinX,
            double boxMaxX,
            double boxMinY,
            double boxMaxY,
            double labelOffset,
            double adminTextHeight,
            int adminLayerIndex)
        {
            if (!TryParseCornerMasterDatum(definitionName, out double angleDegrees, out bool isConcave))
                return;

            double theta = angleDegrees * Math.PI / 180.0;
            Vector3d firstDirection = Vector3d.XAxis;
            Vector3d secondDirection = new Vector3d(Math.Cos(theta), Math.Sin(theta), 0.0);

            var lineAttribs = CreateDottedAdminAttributes(doc, adminLayerIndex);
            AddDatumRayToBoxEdge(
                doc,
                vertex,
                firstDirection,
                boxMinX,
                boxMaxX,
                boxMinY,
                boxMaxY,
                lineAttribs);
            AddDatumRayToBoxEdge(
                doc,
                vertex,
                secondDirection,
                boxMinX,
                boxMaxX,
                boxMinY,
                boxMaxY,
                lineAttribs);

            Vector3d smallerSectorBisector = firstDirection + secondDirection;
            smallerSectorBisector.Z = 0.0;
            if (!smallerSectorBisector.Unitize())
                return;

            Vector3d insideDirection = isConcave
                ? -smallerSectorBisector
                : smallerSectorBisector;
            Vector3d outsideDirection = -insideDirection;

            AddText(
                doc,
                "outside",
                vertex + outsideDirection * labelOffset,
                adminTextHeight,
                adminLayerIndex);

            AddText(
                doc,
                "inside",
                vertex + insideDirection * labelOffset,
                adminTextHeight,
                adminLayerIndex);
        }

        private static bool TryParseCornerMasterDatum(
            string definitionName,
            out double angleDegrees,
            out bool isConcave)
        {
            angleDegrees = 0.0;
            isConcave = false;

            if (string.IsNullOrEmpty(definitionName))
                return false;

            int separator = definitionName.LastIndexOf("::", StringComparison.Ordinal);
            if (separator < 0 || separator + 2 >= definitionName.Length)
                return false;

            string suffix = definitionName.Substring(separator + 2);
            const string concavePrefix = "Concave_";
            const string convexPrefix = "Convex_";
            int angleStart;

            if (suffix.StartsWith(concavePrefix, StringComparison.Ordinal))
            {
                isConcave = true;
                angleStart = concavePrefix.Length;
            }
            else if (suffix.StartsWith(convexPrefix, StringComparison.Ordinal))
            {
                angleStart = convexPrefix.Length;
            }
            else
            {
                return false;
            }

            int heightMarker = suffix.IndexOf("_H", angleStart, StringComparison.Ordinal);
            if (heightMarker <= angleStart)
                return false;

            string angleText = suffix.Substring(angleStart, heightMarker - angleStart);
            return double.TryParse(
                angleText,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out angleDegrees);
        }

        private static void AddDatumRayToBoxEdge(
            RhinoDoc doc,
            Point3d origin,
            Vector3d direction,
            double boxMinX,
            double boxMaxX,
            double boxMinY,
            double boxMaxY,
            ObjectAttributes attributes)
        {
            if (!direction.Unitize())
                return;

            double distance = double.PositiveInfinity;

            if (direction.X > 1e-9)
                distance = Math.Min(distance, (boxMaxX - origin.X) / direction.X);
            else if (direction.X < -1e-9)
                distance = Math.Min(distance, (boxMinX - origin.X) / direction.X);

            if (direction.Y > 1e-9)
                distance = Math.Min(distance, (boxMaxY - origin.Y) / direction.Y);
            else if (direction.Y < -1e-9)
                distance = Math.Min(distance, (boxMinY - origin.Y) / direction.Y);

            if (double.IsInfinity(distance) || distance <= 0.0)
                return;

            doc.Objects.AddCurve(
                new LineCurve(origin, origin + direction * distance),
                attributes);
        }

        private static ObjectAttributes CreateDottedAdminAttributes(RhinoDoc doc, int adminLayerIndex)
        {
            return new ObjectAttributes
            {
                LayerIndex = adminLayerIndex,
                LinetypeSource = ObjectLinetypeSource.LinetypeFromObject,
                LinetypeIndex = GetOrCreateDottedLinetype(doc)
            };
        }

        private static int FindFacadeMasterRow(RhinoDoc doc, int mastersLayerIndex, int facadeBlockDefIndex)
        {
            double boxSize = UnitConverter.MillimetersToModel(doc, BoxSizeMm);
            double boxStartY = UnitConverter.MillimetersToModel(doc, BoxStartYMm);
            double firstFacadeY = boxStartY + boxSize * 0.5;

            foreach (var obj in doc.Objects)
            {
                if (obj.Attributes.LayerIndex != mastersLayerIndex || !(obj is InstanceObject instance))
                    continue;

                if (instance.InstanceDefinition == null || instance.InstanceDefinition.Index != facadeBlockDefIndex)
                    continue;

                double y = instance.InstanceXform.M13;
                return Math.Max(0, (int)Math.Round((firstFacadeY - y) / boxSize));
            }

            return -1;
        }

        private static int CountAssociatedMastersForFacade(
            RhinoDoc doc,
            int mastersLayerIndex,
            string moduleName)
        {
            string cornerPrefix = "Lichen::CornerPlaceholder::" + moduleName + "::";
            string gapPrefix = "Lichen::GapPlaceholder::" + moduleName + "::";
            int count = 0;

            foreach (var obj in doc.Objects)
            {
                if (obj.Attributes.LayerIndex != mastersLayerIndex || !(obj is InstanceObject instance))
                    continue;

                string name = instance.InstanceDefinition?.Name ?? string.Empty;
                if (name.StartsWith(cornerPrefix, StringComparison.Ordinal) ||
                    name.StartsWith(gapPrefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public static int GetOrCreateGapPlaceholderDefinition(
            RhinoDoc doc,
            FacadeModule module,
            double width,
            double height,
            double depthInside,
            double depthOutside)
        {
            double mmPerModelUnit = 1.0 / UnitConverter.MillimetersToModel(doc, 1.0);
            double roundedWidthMm = Math.Round(width * mmPerModelUnit);
            double roundedHeightMm = Math.Round(height * mmPerModelUnit);

            string blockName = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Lichen::GapPlaceholder::{0}::W{1:0}_H{2:0}",
                module.Name,
                roundedWidthMm,
                roundedHeightMm);

            var existing = doc.InstanceDefinitions.Find(blockName);
            if (existing != null)
                return existing.Index;

            var geometries = CreateWireframeBox(
                width,
                -depthInside,
                depthOutside,
                height);
            int gapsLayerIndex = EnsureGapHelplinesLayer(doc);
            var attributes = new List<ObjectAttributes>();
            foreach (var geometry in geometries)
                attributes.Add(CreatePlaceholderAttributes(gapsLayerIndex));

            int definitionIndex = doc.InstanceDefinitions.Add(
                blockName,
                module.Name + " horizontal gap placeholder",
                Point3d.Origin,
                geometries,
                attributes);

            if (definitionIndex >= 0)
                RhinoApp.WriteLine("Lichen: created gap placeholder definition {0}", blockName);

            return definitionIndex;
        }

        public static void EnsureGapMaster(
            RhinoDoc doc,
            FacadeModule module,
            int facadeBlockDefIndex,
            int gapBlockDefIndex)
        {
            EnsureAssociatedMaster(
                doc,
                module,
                facadeBlockDefIndex,
                gapBlockDefIndex,
                "gap");
        }

        private static ObjectAttributes CreatePlaceholderAttributes(int layerIndex)
        {
            return new ObjectAttributes
            {
                LayerIndex = layerIndex,
                ColorSource = ObjectColorSource.ColorFromLayer
            };
        }

        private static List<GeometryBase> CreateWireframeBox(
            double width,
            double minY,
            double maxY,
            double height)
        {
            var result = new List<GeometryBase>();
            var bottom = new[]
            {
                new Point3d(0.0, minY, 0.0),
                new Point3d(width, minY, 0.0),
                new Point3d(width, maxY, 0.0),
                new Point3d(0.0, maxY, 0.0)
            };
            var top = new Point3d[4];
            for (int i = 0; i < 4; i++)
                top[i] = bottom[i] + Vector3d.ZAxis * height;

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                result.Add(new LineCurve(bottom[i], bottom[next]));
                result.Add(new LineCurve(top[i], top[next]));
                result.Add(new LineCurve(bottom[i], top[i]));
            }

            return result;
        }

        private static bool TryIntersectPlanLines(
            Point3d a,
            Vector3d aDirection,
            Point3d b,
            Vector3d bDirection,
            out Point3d intersection)
        {
            double cross = aDirection.X * bDirection.Y - aDirection.Y * bDirection.X;
            if (Math.Abs(cross) < 1e-9)
            {
                intersection = Point3d.Unset;
                return false;
            }

            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double t = (dx * bDirection.Y - dy * bDirection.X) / cross;
            intersection = a + aDirection * t;
            intersection.Z = 0.0;
            return true;
        }

        // ─── helpers ──────────────────────────────────────────────────────────────

        private static int CountExistingFacadeMasters(RhinoDoc doc, int mastersLayerIndex)
        {
            int count = 0;
            foreach (var obj in doc.Objects)
            {
                if (obj.Attributes.LayerIndex != mastersLayerIndex || !(obj is InstanceObject instance))
                    continue;

                string name = instance.InstanceDefinition?.Name ?? string.Empty;
                bool isFacade =
                    (name.StartsWith("Lichen::", StringComparison.Ordinal) ||
                     name.StartsWith("Lichen_Custom_", StringComparison.Ordinal)) &&
                    !name.StartsWith("Lichen::CornerPlaceholder::", StringComparison.Ordinal) &&
                    !name.StartsWith("Lichen::GapPlaceholder::", StringComparison.Ordinal);

                if (isFacade)
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