using Rhino;
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

        public LichenCatalogue(string facadesFolder)
        {
            _facadesFolder = facadesFolder;
        }

        public IReadOnlyList<FacadeModule> Modules => _modules.AsReadOnly();

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
    }
}