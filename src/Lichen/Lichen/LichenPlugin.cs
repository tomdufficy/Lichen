using Lichen.Core;
using Rhino;
using Rhino.PlugIns;
using System.IO;

namespace Lichen
{
    public class LichenPlugin : PlugIn
    {
        public LichenPlugin()
        {
            Instance = this;
        }

        public static LichenPlugin Instance { get; private set; }

        public LichenCatalogue Catalogue { get; private set; }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            string pluginPath = GetType().Assembly.Location;
            string pluginFolder = Path.GetDirectoryName(pluginPath);

            if (string.IsNullOrEmpty(pluginFolder))
            {
                errorMessage = "Lichen could not determine the plug-in folder.";
                return LoadReturnCode.ErrorShowDialog;
            }

            string facadesFolder = Path.Combine(pluginFolder, "facades");

            Catalogue = new LichenCatalogue(facadesFolder);
            Catalogue.Load();

            RhinoApp.WriteLine("Lichen loaded.");
            return LoadReturnCode.Success;
        }
    }
}