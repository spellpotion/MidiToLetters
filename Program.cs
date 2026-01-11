using System.Runtime.InteropServices;
using NAudio.Midi;


namespace MidiToLetters
{
    public static class Program
    {
        private const string ConfigFile = "appsettings.json";
        private const ushort VK_SPACE = 0x20;

        private static int? noteNumberPrev = null;
        private static int? pendingBlackNoteNumber = null;
        private static int? pendingBlackPitch = null;
        private static readonly Dictionary<int, bool> sharpToggle = [];
        private static AppConfig config = new();

        public static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (!File.Exists(ConfigFile))
            {
                Console.WriteLine($"{ConfigFile} not found. Creating default config.");
                var defaultConfig = AppConfig.CreateDefault();
                defaultConfig.Save(ConfigFile);
            }

            config = AppConfig.Load(ConfigFile);

            PrintMidiDevices();

            if (config.MidiDeviceIndex < 0 || config.MidiDeviceIndex >= MidiIn.NumberOfDevices)
            {
                Console.WriteLine($"MidiDeviceIndex {config.MidiDeviceIndex} is invalid.");
                return;
            }

            Console.WriteLine($"Using MIDI input device index: {config.MidiDeviceIndex} ({MidiIn.DeviceInfo(config.MidiDeviceIndex).ProductName})");
            Console.WriteLine($"Enharmonic Mode: {config.ParsedMode}");
            Console.WriteLine($"Tap Mode: {config.ParsedTap}");
            Console.WriteLine("Press Ctrl+C to exit.\n");

            using var midiIn = new MidiIn(config.MidiDeviceIndex);
            midiIn.MessageReceived += OnMessageReceived;
            midiIn.ErrorReceived += (_, e) => Console.WriteLine($"MIDI Error: {e.RawMessage}");
            midiIn.Start();

            Thread.Sleep(Timeout.Infinite);
        }

        private static void PrintMidiDevices()
        {
            Console.WriteLine("Available MIDI Input Devices:");
            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                var info = MidiIn.DeviceInfo(i);
                Console.WriteLine($"  [{i}] {info.ProductName}");
            }
            Console.WriteLine();
        }

        private static void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            if (e.MidiEvent is NoteOnEvent noteOnEvent)
            {
                TryReloadConfig();

                if (config.ParsedTap == TapMode.Exclusive || config.ParsedTap == TapMode.Combined)
                {
                    SendSpaceKey();

                    Console.WriteLine($"MIDI NoteOn → VK_SPACE");

                    if (config.ParsedTap == TapMode.Exclusive) return;
                }

                var noteNumber = noteOnEvent.NoteNumber;
                var pitch = ((noteNumber % 12) + 12) % 12;

                if (config.ParsedMode == EnharmonicMode.Post)
                {
                    if (IsBlack(pitch))
                    {
                        pendingBlackNoteNumber = noteNumber;
                        pendingBlackPitch = pitch;
                        Console.WriteLine($"MIDI {noteNumber} pending");
                        return;
                    }

                    if (pendingBlackNoteNumber.HasValue && pendingBlackPitch.HasValue)
                    {
                        var nameBlack = ResolveNoteNameContextual(
                            pendingBlackPitch.Value,
                            pendingBlackNoteNumber.Value,
                            noteNumber);

                        if (config.Mappings.TryGetValue(nameBlack, out var letterBlack) && !string.IsNullOrWhiteSpace(letterBlack))
                        {
                            var ch = letterBlack.Trim()[0];
                            SendUnicodeChar(ch);

                            Console.WriteLine($"MIDI {noteNumber} ({nameBlack}) -> '{ch}'");
                        }
                        else
                        {
                            Console.WriteLine($"MIDI {noteNumber} ({nameBlack}) no mapping!");
                        }

                        noteNumberPrev = noteNumber;
                        return;
                    }
                }

                string name = ResolveNoteName(noteNumber, config.ParsedMode);

                if (config.Mappings.TryGetValue(name, out var letter) && !string.IsNullOrWhiteSpace(letter))
                {
                    char ch = letter.Trim()[0];
                    SendUnicodeChar(ch);

                    Console.WriteLine($"MIDI {noteNumber} ({name}) -> '{ch}'");
                }
                else
                {
                    Console.WriteLine($"MIDI {noteNumber} ({name}) no mapping!");
                }

                noteNumberPrev = noteNumber;
            }
        }

        private static bool IsBlack(int pc) => pc is 1 or 3 or 6 or 8 or 10;

        private static DateTime lastConfigReadUtc = DateTime.MinValue;
        private static DateTime lastConfigWriteUtc = DateTime.MinValue;

        private static void TryReloadConfig()
        {
            try
            {
                var writeUtc = File.GetLastWriteTimeUtc(ConfigFile);

                if ((DateTime.UtcNow - lastConfigReadUtc).TotalMilliseconds < 250)
                    return;

                lastConfigReadUtc = DateTime.UtcNow;

                if (writeUtc != lastConfigWriteUtc)
                {
                    config = AppConfig.Load(ConfigFile);
                    lastConfigWriteUtc = writeUtc;
                }
            }
            catch { }
        }

        private static string ResolveNoteName(int noteNumber, EnharmonicMode mode)
        {
            var pitch = ((noteNumber % 12) + 12) % 12;

            return pitch switch
            {
                0 => "C",
                2 => "D",
                4 => "E",
                5 => "F",
                7 => "G",
                9 => "A",
                11 => "B",
                1 or 3 or 6 or 8 or 10 => ResolveBlackKey(pitch, noteNumber, mode),
                _ => "H"
            };
        }

        private static string ResolveNoteNameContextual(int pitch, int noteNumberPrev, int noteNumber)
        {
            (string sharp, string flat) = pitch switch
            {
                1 => ("C#", "Db"),
                3 => ("D#", "Eb"),
                6 => ("F#", "Gb"),
                8 => ("G#", "Ab"),
                10 => ("A#", "Bb"),
                _ => ("H#", "Hb")
            };

            return noteNumberPrev < noteNumber ? flat : sharp;
        }

        private static string ResolveBlackKey(int pitch, int noteNumber, EnharmonicMode mode)
        {
            (string sharp, string flat) = pitch switch
            {
                1 => ("C#", "Db"),
                3 => ("D#", "Eb"),
                6 => ("F#", "Gb"),
                8 => ("G#", "Ab"),
                10 => ("A#", "Bb"),
                _ => ("H#", "Hb")
            };

            if (mode == EnharmonicMode.Cycle)
            {
                if (!sharpToggle.TryGetValue(pitch, out bool sharpNext))
                {
                    sharpNext = true;
                }

                sharpToggle[pitch] = !sharpNext;

                return sharpNext ? sharp : flat;
            }

            if (mode == EnharmonicMode.Ante)
            {
                if (noteNumberPrev is null) return sharp;

                return noteNumberPrev > noteNumber ? flat : sharp;
            }

            return sharp;
        }

        private const uint INPUT_KEYBOARD = 1;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private static void SendUnicodeChar(char ch)
        {
            var inputs = new INPUT[2];

            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)ch,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = (ushort)ch,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
            {
                Console.WriteLine($"SendInput failed (sent {sent}/{inputs.Length}), Win32Error={Marshal.GetLastWin32Error()}");
            }
        }

        private static void SendSpaceKey()
        {
            var inputs = new INPUT[2];

            // key down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_SPACE,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // key up
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VK_SPACE,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
            {
                Console.WriteLine($"SendInput(VK_SPACE) failed (sent {sent}/{inputs.Length}), Win32Error={Marshal.GetLastWin32Error()}");
            }
        }

    }
}
