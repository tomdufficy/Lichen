using Lichen.Core;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;

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
                RhinoApp.WriteLine("Lichen: no facade modules found. Use LichenList to check.");
                return Result.Failure;
            }

            // print available modules
            RhinoApp.WriteLine("Lichen: available facade modules:");
            for (int i = 0; i < catalogue.Modules.Count; i++)
            {
                RhinoApp.WriteLine("  {0}. {1}", i + 1, catalogue.Modules[i].Name);
            }

            // ask user to pick a module by number
            var getNumber = new Rhino.Input.Custom.GetInteger();
            getNumber.SetCommandPrompt("Select facade module by number");
            getNumber.SetLowerLimit(1, false);
            getNumber.SetUpperLimit(catalogue.Modules.Count, false);
            if (getNumber.Get() != Rhino.Input.GetResult.Number)
            {
                RhinoApp.WriteLine("Lichen: no module selected.");
                return Result.Cancel;
            }

            int moduleIndex = getNumber.Number() - 1;
            FacadeModule selectedModule = catalogue.Modules[moduleIndex];
            RhinoApp.WriteLine("Lichen: using module {0}", selectedModule.Name);

            // import block definition and place master if needed
            bool isNewBlock = !BlockManager.BlockExists(doc, selectedModule.Name);
            int blockDefIndex = BlockManager.ImportBlock(doc, selectedModule);

            if (blockDefIndex < 0)
            {
                RhinoApp.WriteLine("Lichen: failed to import block. Aborting.");
                return Result.Failure;
            }

            if (isNewBlock)
            {
                BlockManager.PlaceMaster(doc, selectedModule, blockDefIndex);
            }

            // ask user to select volumes
            var getObject = new Rhino.Input.Custom.GetObject();
            getObject.SetCommandPrompt("Select building volume(s)");
            getObject.GeometryFilter = ObjectType.Brep;
            getObject.GetMultiple(1, 0);

            if (getObject.CommandResult() != Result.Success)
            {
                RhinoApp.WriteLine("Lichen: no volume selected.");
                return Result.Cancel;
            }

            // process each selected volume
            for (int i = 0; i < getObject.ObjectCount; i++)
            {
                var brep = getObject.Object(i).Brep();
                if (brep == null) continue;

                RhinoApp.WriteLine("Lichen: processing volume {0} of {1}",
                    i + 1, getObject.ObjectCount);

                var wallFaces = FacadePlacer.GetWallFaces(brep);
                RhinoApp.WriteLine("Lichen: found {0} wall face(s)", wallFaces.Count);

                foreach (var face in wallFaces)
                {
                    FacadePlacer.PlaceFacadesOnFace(doc, face, blockDefIndex);
                }
            }

            RhinoApp.WriteLine("Lichen: done.");
            return Result.Success;
        }
    }
}