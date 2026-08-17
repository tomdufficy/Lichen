using Lichen.Core;
using Lichen.UI;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;

namespace Lichen.Commands
{
    public class LichenApplyCommand : Command
    {
        public LichenApplyCommand()
        {
            Instance = this;
        }

        public static LichenApplyCommand Instance { get; private set; }

        public override string EnglishName => "LichenApply";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var catalogue = LichenPlugin.Instance.Catalogue;

            if (catalogue.Modules.Count == 0)
            {
                RhinoApp.WriteLine(
                    "Lichen: no facade modules found. Use LichenList to check.");

                return Result.Failure;
            }

            var dialog = new FacadeBrowserDialog(
                catalogue.Modules,
                browseOnly: false);

            dialog.ShowModal(
                Rhino.UI.RhinoEtoApp.MainWindowForDocument(doc));

            if (!dialog.WasApplied ||
                dialog.SelectedModule == null)
            {
                RhinoApp.WriteLine("Lichen: no module selected.");
                return Result.Cancel;
            }

            FacadeModule selectedModule = dialog.SelectedModule;

            var placementOptions = new FacadePlacementOptions
            {
                StretchVertically = dialog.StretchVertically,
                StretchHorizontally = dialog.StretchHorizontally,
                HorizontalAlignment = dialog.HorizontalAlignment
            };

            bool generateCornerPlaceholders = dialog.GenerateCornerPlaceholders;

            var slabOptions = new SlabGenerationOptions
            {
                GenerateFloorSlabs = dialog.GenerateFloorSlabs,
                GenerateCeilingSlabs = dialog.GenerateCeilingSlabs,
                FloorThicknessMm = dialog.FloorSlabThicknessMm,
                CeilingThicknessMm = dialog.CeilingSlabThicknessMm
            };

            RhinoApp.WriteLine(
                "Lichen: using module {0}",
                selectedModule.Name);

            var getObject = new Rhino.Input.Custom.GetObject();

            getObject.SetCommandPrompt(
                "Select building volume(s) with planar vertical facade faces");

            getObject.GeometryFilter = ObjectType.Brep;
            getObject.GetMultiple(1, 0);

            if (getObject.CommandResult() != Result.Success)
            {
                RhinoApp.WriteLine("Lichen: no volume selected.");
                return Result.Cancel;
            }

            bool isNewBlock =
                !BlockManager.BlockExists(
                    doc,
                    selectedModule.Name);

            int blockDefIndex =
                BlockManager.ImportBlock(
                    doc,
                    selectedModule);

            if (blockDefIndex < 0)
            {
                RhinoApp.WriteLine(
                    "Lichen: failed to import block. Aborting.");

                return Result.Failure;
            }


            // Facade modules are authored with Y = 0 on the facade line.
            // FacadePlacer maps local +Y outward, so only geometry extending
            // into negative local Y occupies the building interior.
            double inwardFacadeDepth = 0.0;
            var blockDefinition = doc.InstanceDefinitions[blockDefIndex];
            if (blockDefinition != null)
            {
                BoundingBox moduleBounds = BoundingBox.Empty;
                foreach (var obj in blockDefinition.GetObjects())
                    moduleBounds.Union(obj.Geometry.GetBoundingBox(true));

                if (moduleBounds.IsValid && moduleBounds.Min.Y < 0.0)
                    inwardFacadeDepth = -moduleBounds.Min.Y;
            }

            RhinoApp.WriteLine(
                "Lichen: facade inward depth = {0:G6} model units",
                inwardFacadeDepth);

            if (isNewBlock)
            {
                BlockManager.PlaceMaster(
                    doc,
                    selectedModule,
                    blockDefIndex);
            }

            for (int i = 0;
                 i < getObject.ObjectCount;
                 i++)
            {
                var brep = getObject.Object(i).Brep();

                if (brep == null)
                    continue;

                RhinoApp.WriteLine(
                    "Lichen: processing volume {0} of {1}",
                    i + 1,
                    getObject.ObjectCount);

                if (FacadePlacer.HasUnsupportedCurvedFacadeFaces(
                    brep,
                    doc.ModelAbsoluteTolerance))
                {
                    RhinoApp.WriteLine(
                        "Lichen: volume skipped. Curved facade faces are not supported; facade faces must be planar and vertical.");

                    continue;
                }

                var wallFaces =
                    FacadePlacer.GetWallFaces(
                        brep,
                        doc.ModelAbsoluteTolerance);

                if (wallFaces.Count == 0)
                {
                    RhinoApp.WriteLine(
                        "Lichen: volume skipped. No planar vertical facade faces were found.");

                    continue;
                }

                RhinoApp.WriteLine(
                    "Lichen: found {0} planar vertical facade face(s)",
                    wallFaces.Count);

                var placedSpansByFace =
                    new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<FacadeVerticalSpan>>();

                foreach (var face in wallFaces)
                {
                    var verticalSpans = FacadePlacer.PlaceFacadesOnFace(
                        doc,
                        face,
                        blockDefIndex,
                        placementOptions);

                    placedSpansByFace[face.FaceIndex] = verticalSpans;
                }

                if (generateCornerPlaceholders)
                {
                    int cornerCount = CornerGenerator.GenerateForVolume(
                        doc,
                        brep,
                        selectedModule,
                        blockDefIndex,
                        placedSpansByFace);

                    RhinoApp.WriteLine(
                        "Lichen: placed {0} corner placeholder(s)",
                        cornerCount);
                }

                SlabGenerator.GenerateForVolume(
                    doc,
                    brep,
                    slabOptions,
                    inwardFacadeDepth);
            }

            RhinoApp.WriteLine("Lichen: done.");
            return Result.Success;
        }
    }
}