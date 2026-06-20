using Lichen.Core;
using Rhino;
using Rhino.Commands;

namespace Lichen.Commands
{
    public class LichenListCommand : Command
    {
        public LichenListCommand()
        {
            Instance = this;
        }

        public static LichenListCommand Instance { get; private set; }

        public override string EnglishName => "LichenList";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var catalogue = LichenPlugin.Instance.Catalogue;

            if (catalogue.Modules.Count == 0)
            {
                RhinoApp.WriteLine("Lichen: no facade modules found.");
                return Result.Success;
            }

            RhinoApp.WriteLine("Lichen: available facade modules:");

            for (int i = 0; i < catalogue.Modules.Count; i++)
            {
                RhinoApp.WriteLine("  {0}. {1}", i + 1, catalogue.Modules[i].Name);
            }

            return Result.Success;
        }
    }
}