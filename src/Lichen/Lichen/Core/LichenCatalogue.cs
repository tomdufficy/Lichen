using Rhino;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Lichen.Core
{
    public class FacadeModule
    {
        public string Name { get; set; }

        public string FilePath { get; set; }

        public string PreviewPath { get; set; }

        public string Description { get; set; }

        public double WidthMm { get; set; }

        public double HeightMm { get; set; }
    }

    [DataContract]
    internal class FacadeCatalogueEntry
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "widthMm")]
        public double WidthMm { get; set; }

        [DataMember(Name = "heightMm")]
        public double HeightMm { get; set; }

        [DataMember(Name = "preview")]
        public string Preview { get; set; }

        [DataMember(Name = "model")]
        public string Model { get; set; }
    }

    public class LichenCatalogue
    {
        private readonly string _facadesFolder;
        private readonly List<FacadeModule> _modules = new List<FacadeModule>();

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
                RhinoApp.WriteLine(
                    "Lichen: facades folder not found at {0}",
                    _facadesFolder);

                return;
            }

            var pluginFolder = Directory.GetParent(_facadesFolder)?.FullName;

            if (string.IsNullOrWhiteSpace(pluginFolder))
            {
                RhinoApp.WriteLine("Lichen: could not determine the plugin folder.");
                return;
            }

            var catalogueFolder = Path.Combine(pluginFolder, "facade-library");
            var cataloguePath = Path.Combine(catalogueFolder, "facades.json");

            if (!File.Exists(cataloguePath))
            {
                RhinoApp.WriteLine(
                    "Lichen: facade catalogue not found at {0}",
                    cataloguePath);

                return;
            }

            try
            {
                Dictionary<string, FacadeCatalogueEntry> entries;

                var serializer =
                    new DataContractJsonSerializer(
                        typeof(Dictionary<string, FacadeCatalogueEntry>),
                        new DataContractJsonSerializerSettings
                        {
                            UseSimpleDictionaryFormat = true
                        });

                using (var stream = File.OpenRead(cataloguePath))
                {
                    entries =
                        serializer.ReadObject(stream)
                        as Dictionary<string, FacadeCatalogueEntry>;
                }

                if (entries == null)
                {
                    RhinoApp.WriteLine("Lichen: facade catalogue is empty.");
                    return;
                }

                foreach (var pair in entries)
                {
                    var entry = pair.Value;

                    if (entry == null)
                        continue;

                    var name = string.IsNullOrWhiteSpace(entry.Name)
                        ? pair.Key
                        : entry.Name;

                    var modelFileName = string.IsNullOrWhiteSpace(entry.Model)
                        ? name + ".3dm"
                        : entry.Model;

                    var previewFileName = string.IsNullOrWhiteSpace(entry.Preview)
                        ? name + ".png"
                        : entry.Preview;

                    var modelPath = Path.Combine(_facadesFolder, modelFileName);
                    var previewPath = Path.Combine(catalogueFolder, previewFileName);

                    if (!File.Exists(modelPath))
                    {
                        RhinoApp.WriteLine(
                            "Lichen: facade model not found: {0}",
                            modelPath);

                        continue;
                    }

                    _modules.Add(new FacadeModule
                    {
                        Name = name,
                        FilePath = modelPath,
                        PreviewPath = File.Exists(previewPath)
                            ? previewPath
                            : null,
                        Description = entry.Description ?? string.Empty,
                        WidthMm = entry.WidthMm,
                        HeightMm = entry.HeightMm
                    });
                }

                _modules.Sort(
                    (a, b) => string.Compare(
                        a.Name,
                        b.Name,
                        StringComparison.OrdinalIgnoreCase));

                RhinoApp.WriteLine(
                    "Lichen: loaded {0} facade module(s).",
                    _modules.Count);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine(
                    "Lichen: failed to read facade catalogue: {0}",
                    ex.Message);
            }
        }
    }
}