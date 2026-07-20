using Rhino;
using System.Collections.Generic;
using System.IO;

namespace Lichen.Core
{
    public class FacadeModule
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string PreviewPath { get; set; }
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

            var pluginFolder = Directory.GetParent(_facadesFolder)?.FullName;
            var previewFolder = pluginFolder == null
                ? null
                : Path.Combine(pluginFolder, "facade-library");

            foreach (var file in Directory.GetFiles(_facadesFolder, "*.3dm"))
            {
                var name = Path.GetFileNameWithoutExtension(file);

                var previewPath = previewFolder == null
                    ? null
                    : Path.Combine(previewFolder, name + ".png");

                _modules.Add(new FacadeModule
                {
                    Name = name,
                    FilePath = file,
                    PreviewPath = File.Exists(previewPath) ? previewPath : null
                });
            }

            RhinoApp.WriteLine("Lichen: loaded {0} facade module(s).", _modules.Count);
        }
    }
}