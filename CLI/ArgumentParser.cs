using System;
using System.IO.Ports;

namespace Wio.CLI;

public class WioOptions
{
    public string? Target { get; set; }
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public Handshake FlowControl { get; set; } = Handshake.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Parity Parity { get; set; } = Parity.None;
    public bool NoReconnect { get; set; } = false;
    public bool LocalEcho { get; set; } = false;
    public bool ListDevices { get; set; } = false;
    public bool ShowHelp { get; set; } = false;
    public bool ShowVersion { get; set; } = false;
}

public static class ArgumentParser
{
    public static WioOptions? Parse(string[] args)
    {
        var options = new WioOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg == "-h" || arg == "--help")
            {
                options.ShowHelp = true;
                return options;
            }
            else if (arg == "-v" || arg == "--version")
            {
                options.ShowVersion = true;
                return options;
            }
            else if (arg == "-l" || arg == "--list")
            {
                options.ListDevices = true;
            }
            else if (arg == "-n" || arg == "--no-reconnect")
            {
                options.NoReconnect = true;
            }
            else if (arg == "-e" || arg == "--local-echo")
            {
                options.LocalEcho = true;
            }
            else if (arg == "-b" || arg == "--baudrate")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Error: Missing value for baudrate option.");
                    return null;
                }
                if (!int.TryParse(args[++i], out int baud) || baud <= 0)
                {
                    Console.Error.WriteLine($"Error: Invalid baudrate: {args[i]}");
                    return null;
                }
                options.BaudRate = baud;
            }
            else if (arg == "-d" || arg == "--databits")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Error: Missing value for databits option.");
                    return null;
                }
                if (!int.TryParse(args[++i], out int db) || db < 5 || db > 8)
                {
                    Console.Error.WriteLine($"Error: Invalid databits: {args[i]} (must be 5, 6, 7, or 8)");
                    return null;
                }
                options.DataBits = db;
            }
            else if (arg == "-f" || arg == "--flow")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Error: Missing value for flow control option.");
                    return null;
                }
                string val = args[++i].ToLowerInvariant();
                options.FlowControl = val switch
                {
                    "hard" => Handshake.RequestToSend,
                    "soft" => Handshake.XOnXOff,
                    "none" => Handshake.None,
                    _ => Handshake.None
                };
                if (val != "hard" && val != "soft" && val != "none")
                {
                    Console.Error.WriteLine($"Warning: Unknown flow control: {val}, defaulting to 'none'");
                }
            }
            else if (arg == "-s" || arg == "--stopbits")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Error: Missing value for stopbits option.");
                    return null;
                }
                string val = args[++i];
                options.StopBits = val switch
                {
                    "1" => StopBits.One,
                    "2" => StopBits.Two,
                    "1.5" => StopBits.OnePointFive,
                    _ => StopBits.One
                };
                if (val != "1" && val != "2" && val != "1.5")
                {
                    Console.Error.WriteLine($"Warning: Unknown stopbits: {val}, defaulting to '1'");
                }
            }
            else if (arg == "-p" || arg == "--parity")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Error: Missing value for parity option.");
                    return null;
                }
                string val = args[++i].ToLowerInvariant();
                options.Parity = val switch
                {
                    "odd" => Parity.Odd,
                    "even" => Parity.Even,
                    "none" => Parity.None,
                    "mark" => Parity.Mark,
                    "space" => Parity.Space,
                    _ => Parity.None
                };
                if (val != "odd" && val != "even" && val != "none" && val != "mark" && val != "space")
                {
                    Console.Error.WriteLine($"Warning: Unknown parity: {val}, defaulting to 'none'");
                }
            }
            else if (arg.StartsWith("-"))
            {
                Console.Error.WriteLine($"Error: Unknown option: {arg}");
                return null;
            }
            else
            {
                // Positional argument -> Target device
                if (options.Target != null)
                {
                    Console.Error.WriteLine($"Error: Multiple target devices specified: {options.Target} and {arg}");
                    return null;
                }
                options.Target = arg;
            }
        }

        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine(@"Usage: wio [<options>] <tty-device|profile|tid>

Connect to TTY device directly or via configuration profile or topology ID.

Options:
  -b, --baudrate <bps>                   Baud rate (default: 115200)
  -d, --databits 5|6|7|8                 Data bits (default: 8)
  -f, --flow hard|soft|none              Flow control (default: none)
  -s, --stopbits 1|2                     Stop bits (default: 1)
  -p, --parity odd|even|none|mark|space  Parity (default: none)
  -n, --no-reconnect                     Do not reconnect
  -e, --local-echo                       Enable local echo
  -l, --list                             List available serial devices
  -v, --version                          Display version
  -h, --help                             Display help");
    }
}
