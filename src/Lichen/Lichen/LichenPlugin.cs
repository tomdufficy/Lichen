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
            string facadesFolder = Path.Combine(Path.GetDirectoryName(PlugIn.PathFromName("Lichen")), "facades");

            Catalogue = new LichenCatalogue(facadesFolder);
            Catalogue.Load();

            RhinoApp.WriteLine("Lichen loaded.");
            return LoadReturnCode.Success;
        }
    }
}