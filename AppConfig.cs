using System.Text.Json;

namespace MidiToLetters
{
    public sealed class AppConfig
    {
        private static readonly JsonSerializerOptions CachedJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public int MidiDeviceIndex { get; set; } = 0;
        public string Mode { get; set; } = "Ante";
        public Dictionary<string, string> Mappings { get; set; } = [];

        public EnharmonicMode ParsedMode =>
            Enum.TryParse<EnharmonicMode>(Mode, ignoreCase: true, out var m) ? m : EnharmonicMode.Ante;

        public static AppConfig Load(string path)
        {
            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, CachedJsonOptions);
            return cfg ?? new AppConfig();
        }
    }

    public enum EnharmonicMode
    {
        Ante,
        Post,
        Cycle
    }
}
