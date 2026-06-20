using Rhino;
using Rhino.FileIO;
using System;
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

            var files = Directory.GetFiles(_facadesFolder, "*.3dm");

            foreach (var file in files)
            {
                var module = new FacadeModule
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                };
                _modules.Add(module);
            }

            RhinoApp.WriteLine("Lichen: loaded {0} facade module(s).", _modules.Count);
        }
    }
}