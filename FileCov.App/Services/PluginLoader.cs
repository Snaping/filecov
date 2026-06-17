using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using FileCov.Contracts;

namespace FileCov.App.Services;

public class PluginLoader
{
    [ImportMany]
    private IEnumerable<Lazy<IConverter, IDictionary<string, object>>> _converters = null!;

    private CompositionContainer? _container;

    public void LoadPlugins(string? pluginPath = null)
    {
        var catalog = new AggregateCatalog();
        catalog.Catalogs.Add(new ApplicationCatalog());

        if (pluginPath != null && Directory.Exists(pluginPath))
        {
            catalog.Catalogs.Add(new DirectoryCatalog(pluginPath!));
        }

        _container = new CompositionContainer(catalog);
        var batch = new CompositionBatch();
        batch.AddPart(this);
        _container.Compose(batch);
    }

    public IReadOnlyList<IConverter> GetConverters()
    {
        return _converters.Select(c => c.Value).ToList().AsReadOnly();
    }

    public IConverter? GetConverterForFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return _converters.FirstOrDefault(c =>
            c.Value.SupportedInputExtensions.Any(e =>
                e.Equals(ext, StringComparison.OrdinalIgnoreCase)))?.Value;
    }

    public IConverter? GetConverterByName(string name)
    {
        return _converters.FirstOrDefault(c =>
            c.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}
