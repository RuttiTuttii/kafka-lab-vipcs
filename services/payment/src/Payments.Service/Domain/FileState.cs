using System.Text.Json;

namespace Payments.Service.Domain;

// состояние в json-файле: атомарная запись через временный файл, чтобы данные переживали перезапуск
public sealed class FileState<T>(string? path) where T : class, new()
{
    private static readonly JsonSerializerOptions Opt = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public bool Exists => path is not null && File.Exists(path);

    public T Load()
    {
        if (!Exists) return new T();
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path!), Opt) ?? new T(); }
        catch (JsonException) { return new T(); }
    }

    public void Save(T state)
    {
        if (path is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Opt));
        File.Move(tmp, path, overwrite: true);
    }
}
