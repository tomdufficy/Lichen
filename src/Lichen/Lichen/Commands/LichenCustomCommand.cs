using Lichen.Core;
using Lichen.UI;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Collections.Generic;

namespace Lichen.Commands
{
    public class LichenCustomCommand : Command
    {
        public LichenCustomCommand()
        {
            Instance = this;
        }

        public static LichenCustomCommand Instance { get; private set; }

        public override string EnglishName => "LichenCustom";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var dialog = new CustomFacadeDialog(doc);
            dialog.ShowModal(
                Rhino.UI.RhinoEtoApp.MainWindowForDocument(doc));

            if (!dialog.WasApplied)
            {
                RhinoApp.WriteLine("Lichen: custom facade cancelled.");
                return Result.Cancel;
            }

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

            var validVolumes = new List<Brep>();
            for (int i = 0; i < getObject.ObjectCount; i++)
            {
                Brep brep = getObject.Object(i).Brep();
                if (brep == null)
                    continue;

                if (FacadePlacer.HasUnsupportedCurvedFacadeFaces(
                    brep,
                    doc.ModelAbsoluteTolerance))
                {
                    RhinoApp.WriteLine(
                        "Lichen: volume skipped. Curved facade faces are not supported; facade faces must be planar and vertical.");
                    continue;
                }

                List<BrepFace> wallFaces = FacadePlacer.GetWallFaces(
                    brep,
                    doc.ModelAbsoluteTolerance);

                if (wallFaces.Count == 0)
                {
                    RhinoApp.WriteLine(
                        "Lichen: volume skipped. No planar vertical facade faces were found.");
                    continue;
                }

                validVolumes.Add(brep);
            }

            if (validVolumes.Count == 0)
            {
                RhinoApp.WriteLine(
                    "Lichen: no valid building volumes selected. No custom facade was created.");
                return Result.Failure;
            }

            var customModule = new FacadeModule
            {
                Name = dialog.BlockName,
                Description = "Custom facade created with LichenCustom.",
                WidthMm = dialog.WidthMm,
                HeightMm = dialog.HeightMm
            };

            int blockDefIndex = BlockManager.GetOrCreateCustomFacadeDefinition(
                doc,
                dialog.BlockName,
                dialog.WidthMm,
                dialog.HeightMm,
                dialog.DepthInsideMm,
                dialog.DepthOutsideMm,
                out bool wasCreated);

            if (blockDefIndex < 0)
            {
                RhinoApp.WriteLine("Lichen: failed to create custom facade block.");
                return Result.Failure;
            }

            if (wasCreated)
            {
                BlockManager.PlaceMaster(
                    doc,
                    customModule,
                    blockDefIndex);
            }

            var placementOptions = new FacadePlacementOptions
            {
                StretchVertically = false,
                StretchHorizontally = false,
                HorizontalPlacement = dialog.HorizontalPlacement,
                CenteredEdgeGaps = dialog.CenteredEdgeGaps
            };

            var slabOptions = new SlabGenerationOptions
            {
                GenerateFloorSlabs = dialog.GenerateFloorSlabs,
                GenerateCeilingSlabs = dialog.GenerateCeilingSlabs,
                FloorThicknessMm = dialog.FloorSlabThicknessMm,
                CeilingThicknessMm = dialog.CeilingSlabThicknessMm
            };

            double inwardFacadeDepth = UnitConverter.MillimetersToModel(
                doc,
                dialog.DepthInsideMm);
            double outwardFacadeDepth = UnitConverter.MillimetersToModel(
                doc,
                dialog.DepthOutsideMm);

            foreach (Brep brep in validVolumes)
            {
                List<BrepFace> wallFaces = FacadePlacer.GetWallFaces(
                    brep,
                    doc.ModelAbsoluteTolerance);

                var placedSpansByFace =
                    new Dictionary<int, List<FacadeVerticalSpan>>();
                var placementsByFace =
                    new Dictionary<int, FacadePlacementResult>();

                foreach (BrepFace face in wallFaces)
                {
                    FacadePlacementResult placement = FacadePlacer.PlaceFacadesOnFace(
                        doc,
                        face,
                        blockDefIndex,
                        placementOptions);

                    placementsByFace[face.FaceIndex] = placement;
                    placedSpansByFace[face.FaceIndex] = placement.VerticalSpans;
                }

                if (dialog.GenerateCornerPlaceholders)
                {
                    int cornerCount = CornerGenerator.GenerateForVolume(
                        doc,
                        brep,
                        customModule,
                        blockDefIndex,
                        placedSpansByFace);

                    RhinoApp.WriteLine(
                        "Lichen: placed {0} corner placeholder(s)",
                        cornerCount);
                }

                if (dialog.GenerateGapFillers)
                {
                    int gapCount = 0;
                    foreach (FacadePlacementResult placement in placementsByFace.Values)
                    {
                        gapCount += GapGenerator.GenerateForFace(
                            doc,
                            customModule,
                            blockDefIndex,
                            placement,
                            inwardFacadeDepth,
                            outwardFacadeDepth,
                            dialog.IncludeEdgeGapFillers);
                    }

                    RhinoApp.WriteLine(
                        "Lichen: placed {0} gap filler placeholder(s)",
                        gapCount);
                }

                SlabGenerator.GenerateForVolume(
                    doc,
                    brep,
                    slabOptions,
                    inwardFacadeDepth);
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine("Lichen: custom facade applied.");
            return Result.Success;
        }
    }
}
