using System;
using System.Runtime.InteropServices;

namespace Wio.Terminal;

public class ConsoleController
{
    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;

    private const uint ENABLE_PROCESSED_INPUT = 0x0001;
    private const uint ENABLE_LINE_INPUT = 0x0002;
    private const uint ENABLE_ECHO_INPUT = 0x0004;
    private const uint ENABLE_WINDOW_INPUT = 0x0008;
    private const uint ENABLE_MOUSE_INPUT = 0x0010;
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;

    private const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
    private const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private readonly IntPtr _hStdin;
    private readonly IntPtr _hStdout;
    private uint _originalStdinMode;
    private uint _originalStdoutMode;
    private bool _stdinModeSaved;
    private bool _stdoutModeSaved;

    public ConsoleController()
    {
        _hStdin = GetStdHandle(STD_INPUT_HANDLE);
        _hStdout = GetStdHandle(STD_OUTPUT_HANDLE);
    }

    public void EnableRawMode()
    {
        // Output VT processing enabling
        if (GetConsoleMode(_hStdout, out uint stdoutMode))
        {
            _originalStdoutMode = stdoutMode;
            _stdoutModeSaved = true;

            // Enable virtual terminal processing for ANSI escape sequences
            uint newStdoutMode = stdoutMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | ENABLE_PROCESSED_OUTPUT;
            SetConsoleMode(_hStdout, newStdoutMode);
        }

        // Input raw mode enabling
        if (GetConsoleMode(_hStdin, out uint stdinMode))
        {
            _originalStdinMode = stdinMode;
            _stdinModeSaved = true;

            // Disable line input (returns immediately), echo input, and processed input (so we get Ctrl-C etc.)
            uint newStdinMode = stdinMode & ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_PROCESSED_INPUT);
            // Ensure extended flags are preserved or set correctly
            newStdinMode |= ENABLE_EXTENDED_FLAGS;
            SetConsoleMode(_hStdin, newStdinMode);
        }

        // Disable standard Ctrl+C interrupt handler in .NET
        Console.TreatControlCAsInput = true;
    }

    public void RestoreMode()
    {
        if (_stdinModeSaved)
        {
            SetConsoleMode(_hStdin, _originalStdinMode);
        }
        if (_stdoutModeSaved)
        {
            SetConsoleMode(_hStdout, _originalStdoutMode);
        }
        Console.TreatControlCAsInput = false;
    }
}
