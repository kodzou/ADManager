using System.Text.Json;

namespace ADManager.Helpers;

public class GroupPresetEntry
{
    public string CN { get; set; } = "";
    public string DN { get; set; } = "";
}

public class GroupPreset
{
    public string Name   { get; set; } = "";
    public string Domain { get; set; } = "";
    public List<GroupPresetEntry> Groups { get; set; } = new();
}

public static class GroupListStorage
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ADManager", "group_presets.json");

    public static List<GroupPreset> Presets { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                Presets = JsonSerializer.Deserialize<List<GroupPreset>>(json) ?? new();
            }
        }
        catch { Presets = new(); }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(Presets, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Logger.Write($"Ошибка сохранения списков групп: {ex.Message}", LogType.Error); }
    }

    public static IEnumerable<GroupPreset> ForDomain(string domain) =>
        Presets.Where(p => string.Equals(p.Domain, domain, StringComparison.OrdinalIgnoreCase));

    public static void AddOrUpdate(GroupPreset preset)
    {
        var existing = Presets.FirstOrDefault(p =>
            string.Equals(p.Name,   preset.Name,   StringComparison.CurrentCultureIgnoreCase) &&
            string.Equals(p.Domain, preset.Domain, StringComparison.OrdinalIgnoreCase));
        if (existing != null) Presets.Remove(existing);
        Presets.Add(preset);
    }

    public static void Remove(string name, string domain)
    {
        Presets.RemoveAll(p =>
            string.Equals(p.Name,   name,   StringComparison.CurrentCultureIgnoreCase) &&
            string.Equals(p.Domain, domain, StringComparison.OrdinalIgnoreCase));
    }
}
