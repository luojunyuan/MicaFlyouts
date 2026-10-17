using System.Runtime.InteropServices;

internal static class Program
{
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    private static readonly Dictionary<string, ushort> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["media-play-pause"] = 0xB3,
        ["media-next"] = 0xB0,
        ["media-previous"] = 0xB1,
        ["media-stop"] = 0xB2,
        ["volume-up"] = 0xAF,
        ["volume-down"] = 0xAE,
        ["volume-mute"] = 0xAD,
        ["caps-lock"] = 0x14,
        ["num-lock"] = 0x90,
        ["scroll-lock"] = 0x91,
        ["insert"] = 0x2D,
    };

    private static int Main(string[] args)
    {
        if (args.Length > 0)
            return RunCommand(args);

        PrintHelp();
        while (true)
        {
            Console.Write("Key> ");
            var command = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(command))
                continue;
            if (command.Equals("q", StringComparison.OrdinalIgnoreCase)
                || command.Equals("quit", StringComparison.OrdinalIgnoreCase)
                || command.Equals("exit", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (command.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            Trigger(command);
        }
    }

    private static int RunCommand(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        var keyName = args.Length == 2 && args[0] == "--key" ? args[1]
            : args.Length == 1 ? args[0]
            : null;

        if (keyName is null)
        {
            Console.Error.WriteLine("Usage: MicaFlyouts.InputSimulator [--key] <key-name>");
            return 2;
        }

        return Trigger(keyName) ? 0 : 1;
    }

    private static bool Trigger(string keyName)
    {
        if (!Keys.TryGetValue(keyName, out var virtualKey))
        {
            Console.WriteLine($"Unknown key '{keyName}'. Type 'help' to list available keys.");
            return false;
        }

        var inputs = new[]
        {
            CreateKeyboardInput(virtualKey, 0),
            CreateKeyboardInput(virtualKey, KeyeventfKeyup),
        };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            Console.WriteLine($"Failed to send '{keyName}' (Win32 error {Marshal.GetLastWin32Error()}).");
            return false;
        }

        Console.WriteLine($"Sent {keyName} (VK 0x{virtualKey:X2}).");
        return true;
    }

    private static Input CreateKeyboardInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = flags,
            },
        },
    };

    private static void PrintHelp()
    {
        Console.WriteLine("MicaFlyouts keyboard input simulator");
        Console.WriteLine("Type a key name to send it, 'help' to show this list, or 'q' to quit.");
        Console.WriteLine("Lock keys change the actual Windows lock state. Volume and media keys can affect other apps.");
        Console.WriteLine();
        foreach (var (name, virtualKey) in Keys)
            Console.WriteLine($"  {name,-20} VK 0x{virtualKey:X2}");
        Console.WriteLine();
        Console.WriteLine("One-shot usage: dotnet run --project tests/MicaFlyouts.InputSimulator -- --key volume-up");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, [In] Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}
