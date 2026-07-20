using Lichen.UI;
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

        public override string EnglishName
        {
            get { return "LichenList"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var catalogue = LichenPlugin.Instance.Catalogue;

            if (catalogue.Modules.Count == 0)
            {
                RhinoApp.WriteLine("Lichen: no facade modules found.");
                return Result.Success;
            }

            var dialog = new FacadeBrowserDialog(catalogue.Modules);

            dialog.ShowModal(
                Rhino.UI.RhinoEtoApp.MainWindowForDocument(doc));

            return Result.Success;
        }
    }
}