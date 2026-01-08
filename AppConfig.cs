using System.Text.Json;

namespace MidiToLetters
{
    public sealed class AppConfig
    {
        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions SerializeOptions = new()
        {
            WriteIndented = true
        };

        public int MidiDeviceIndex { get; set; } = 0;
        public string Mode { get; set; } = "Ante";
        public Dictionary<string, string> Mappings { get; set; } = [];

        public EnharmonicMode ParsedMode =>
            Enum.TryParse<EnharmonicMode>(Mode, ignoreCase: true, out var m) ? m : EnharmonicMode.Ante;

        public static AppConfig Load(string path)
        {
            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, DeserializeOptions);
            return cfg ?? new AppConfig();
        }

        public void Save(string path)
        {
            var json = JsonSerializer.Serialize(this, SerializeOptions);

            File.WriteAllText(path, json);
        }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                MidiDeviceIndex = 0,
                Mode = "Contextual",
                Mappings = new Dictionary<string, string>
                {
                    { "C", "a" },
                    { "D", "s" },
                    { "E", "d" },
                    { "F", "f" },
                    { "G", "g" },
                    { "A", "h" },
                    { "B", "j" },
                    { "C#", "w" },
                    { "Db", "z" },
                    { "D# ", "e" },
                    { "Eb", "x" },
                    { "F#", "t" },
                    { "Gb", "v" },
                    { "G#", "y" },
                    { "Ab", "b" },
                    { "A#", "u" },
                    { "Bb", "n" }
                }
            };
        }
    }
    public enum EnharmonicMode
    {
        Ante,
        Post,
        Cycle
    }
}
