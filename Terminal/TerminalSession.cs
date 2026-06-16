using System;
using System.IO;
using System.Threading;
using Wio.CLI;
using Wio.Engine;

namespace Wio.Terminal;

public class TerminalSession
{
    private readonly WioOptions _options;
    private readonly SerialEngine _engine;
    private readonly ConsoleController _consoleController;
    private volatile bool _isRunning;
    private Thread? _inputThread;
    private bool _prefixActive;
    private readonly Stream _stdoutStream;

    public TerminalSession(WioOptions options)
    {
        _options = options;
        _stdoutStream = Console.OpenStandardOutput();
        
        // Initialize serial engine
        _engine = new SerialEngine(options, OnSerialDataReceived, OnMessage);
        _consoleController = new ConsoleController();
    }

    public void Run()
    {
        _isRunning = true;
        
        // Enable raw console mode
        _consoleController.EnableRawMode();

        try
        {
            // Start the serial connection engine
            _engine.Start();

            // Run the keyboard input thread
            _inputThread = new Thread(InputLoop)
            {
                IsBackground = true,
                Name = "wio-ConsoleInputLoop"
            };
            _inputThread.Start();

            // Wait until stopped
            while (_isRunning)
            {
                Thread.Sleep(100);
            }
        }
        finally
        {
            // Stop threads and restore console
            _engine.Stop();
            _consoleController.RestoreMode();
        }
    }

    private void InputLoop()
    {
        while (_isRunning)
        {
            try
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(10);
                    continue;
                }

                ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

                if (_prefixActive)
                {
                    HandlePrefixKey(keyInfo);
                    _prefixActive = false;
                    continue;
                }

                // Check for prefix key (Ctrl-T by default)
                if (keyInfo.Key == ConsoleKey.T && keyInfo.Modifiers == ConsoleModifiers.Control)
                {
                    _prefixActive = true;
                    continue;
                }

                // Normal key transmission
                SendKey(keyInfo);
            }
            catch (Exception ex)
            {
                OnMessage($"Input read error: {ex.Message}");
                _isRunning = false;
            }
        }
    }

    private void SendKey(ConsoleKeyInfo keyInfo)
    {
        // Convert ConsoleKeyInfo to bytes
        byte[] bytesToSend;

        if (keyInfo.KeyChar != '\0')
        {
            char c = keyInfo.KeyChar;
            
            // Map character if necessary
            // E.g. Enter key is CR (\r) or LF (\n)
            if (c == '\r')
            {
                // tio style CR/LF remapping can be done here. By default send CR.
                bytesToSend = new[] { (byte)'\r' };
            }
            else
            {
                bytesToSend = System.Text.Encoding.UTF8.GetBytes(new[] { c });
            }
        }
        else
        {
            // Special keys (arrows, function keys) could send ANSI escape sequences.
            // For basic terminal, we convert arrow keys to ANSI escape sequences.
            bytesToSend = keyInfo.Key switch
            {
                ConsoleKey.UpArrow => "\u001b[A"u8.ToArray(),
                ConsoleKey.DownArrow => "\u001b[B"u8.ToArray(),
                ConsoleKey.RightArrow => "\u001b[C"u8.ToArray(),
                ConsoleKey.LeftArrow => "\u001b[D"u8.ToArray(),
                ConsoleKey.Home => "\u001b[H"u8.ToArray(),
                ConsoleKey.End => "\u001b[F"u8.ToArray(),
                ConsoleKey.Insert => "\u001b[2~"u8.ToArray(),
                ConsoleKey.Delete => "\u001b[3~"u8.ToArray(),
                ConsoleKey.PageUp => "\u001b[5~"u8.ToArray(),
                ConsoleKey.PageDown => "\u001b[6~"u8.ToArray(),
                _ => Array.Empty<byte>()
            };
        }

        if (bytesToSend.Length > 0)
        {
            // If local echo is enabled, echo to screen
            if (_options.LocalEcho)
            {
                _stdoutStream.Write(bytesToSend, 0, bytesToSend.Length);
                _stdoutStream.Flush();
            }

            _engine.Write(bytesToSend, 0, bytesToSend.Length);
        }
    }

    private void HandlePrefixKey(ConsoleKeyInfo keyInfo)
    {
        char command = char.ToLowerInvariant(keyInfo.KeyChar);

        switch (command)
        {
            case 'q':
                OnMessage("Quitting session...");
                _isRunning = false;
                break;

            case '?':
                ShowPrefixHelp();
                break;

            case 'e':
                _options.LocalEcho = !_options.LocalEcho;
                OnMessage($"Local echo: {(_options.LocalEcho ? "enabled" : "disabled")}");
                break;

            case 'd':
                _engine.ToggleDtr();
                break;

            case 'r':
                _engine.ToggleRts();
                break;

            case 'p':
                OnMessage("Pulse lines menu: [d] DTR, [r] RTS");
                var pulseKey = Console.ReadKey(intercept: true);
                char pulseCmd = char.ToLowerInvariant(pulseKey.KeyChar);
                if (pulseCmd == 'd')
                {
                    OnMessage("Pulsing DTR line...");
                    _engine.PulseDtr(100);
                }
                else if (pulseCmd == 'r')
                {
                    OnMessage("Pulsing RTS line...");
                    _engine.PulseRts(100);
                }
                else
                {
                    OnMessage("Invalid pulse command.");
                }
                break;

            // If Ctrl-T is pressed again, send Ctrl-T (0x14) to the port
            case (char)20: // Ctrl-T character
            case 't':
                byte[] ctrlT = { 20 };
                _engine.Write(ctrlT, 0, 1);
                break;

            default:
                OnMessage($"Unknown session command: ctrl-t {keyInfo.KeyChar}");
                break;
        }
    }

    private void ShowPrefixHelp()
    {
        string helpText = @"
[wio] Session Commands (prefix: ctrl-t):
  ?         Show this help menu
  q         Quit session
  e         Toggle local echo
  d         Toggle DTR line
  r         Toggle RTS line
  p         Pulse line (DTR or RTS)
  t         Send prefix key (ctrl-t) to serial device
";
        OnMessage(helpText);
    }

    private void OnSerialDataReceived(byte[] data, int length)
    {
        lock (_stdoutStream)
        {
            _stdoutStream.Write(data, 0, length);
            _stdoutStream.Flush();
        }
    }

    private void OnMessage(string message)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"\r\n[wio] {message}\r\n");
        lock (_stdoutStream)
        {
            _stdoutStream.Write(bytes, 0, bytes.Length);
            _stdoutStream.Flush();
        }
    }
}
