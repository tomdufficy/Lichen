using Lichen.Core;
using Lichen.UI;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;

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

            // 1. Select facade visually
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

            FacadeModule selectedModule =
                dialog.SelectedModule;

            RhinoApp.WriteLine(
                "Lichen: using module {0}",
                selectedModule.Name);

            // 2. Select volumes
            var getObject = new Rhino.Input.Custom.GetObject();

            getObject.SetCommandPrompt(
                "Select building volume(s) to apply facade to");

            getObject.GeometryFilter = ObjectType.Brep;
            getObject.GetMultiple(1, 0);

            if (getObject.CommandResult() != Result.Success)
            {
                RhinoApp.WriteLine("Lichen: no volume selected.");
                return Result.Cancel;
            }

            // 3. Import block and place master
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

            if (isNewBlock)
            {
                BlockManager.PlaceMaster(
                    doc,
                    selectedModule,
                    blockDefIndex);
            }

            // 4. Apply to each selected volume
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

                var wallFaces =
                    FacadePlacer.GetWallFaces(brep);

                RhinoApp.WriteLine(
                    "Lichen: found {0} wall face(s)",
                    wallFaces.Count);

                foreach (var face in wallFaces)
                {
                    FacadePlacer.PlaceFacadesOnFace(
                        doc,
                        face,
                        blockDefIndex);
                }
            }

            RhinoApp.WriteLine("Lichen: done.");
            return Result.Success;
        }
    }
}