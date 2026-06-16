using System;
using Wio.CLI;
using Wio.Engine;
using Wio.Terminal;

namespace Wio;

class Program
{
    private const string Version = "1.0.0-mvp";

    static void Main(string[] args)
    {
        // Parse arguments
        var options = ArgumentParser.Parse(args);
        if (options == null)
        {
            Environment.Exit(1);
        }

        // Handle help
        if (options.ShowHelp)
        {
            ArgumentParser.PrintHelp();
            return;
        }

        // Handle version
        if (options.ShowVersion)
        {
            Console.WriteLine($"wio version {Version}");
            return;
        }

        // Handle device listing
        if (options.ListDevices)
        {
            DeviceMonitor.PrintDeviceList();
            return;
        }

        // Target resolution
        string? targetPort = ResolveTarget(options.Target);
        if (string.IsNullOrEmpty(targetPort))
        {
            Console.Error.WriteLine("Error: No valid serial port found. Specify a COM port or TID (e.g. 'wio COM3' or 'wio a8B3').");
            Console.Error.WriteLine("Use 'wio -l' to list available serial devices.");
            Environment.Exit(1);
        }

        options.Target = targetPort;

        // Print header
        Console.WriteLine($"[wio] Starting wio {Version}");
        Console.WriteLine($"[wio] Target: {options.Target} | Baudrate: {options.BaudRate}");
        Console.WriteLine("[wio] Press Ctrl-T followed by Q to quit.");
        Console.WriteLine("--------------------------------------------------");

        // Start interactive session
        var session = new TerminalSession(options);
        session.Run();

        Console.WriteLine("\n[wio] Session ended.");
    }

    private static string? ResolveTarget(string? target)
    {
        var devices = DeviceMonitor.GetSerialDevices();

        // 1. If target is empty, try to auto-select
        if (string.IsNullOrEmpty(target))
        {
            if (devices.Count == 0)
            {
                return null;
            }
            if (devices.Count == 1)
            {
                Console.WriteLine($"[wio] Auto-selected single available port: {devices[0].PortName}");
                return devices[0].PortName;
            }

            // More than 1 device, don't guess
            Console.WriteLine($"[wio] Multiple serial ports available:");
            foreach (var d in devices)
            {
                Console.WriteLine($"  * {d.PortName} ({d.Description})");
            }
            return null;
        }

        // 2. If target matches a port name exactly (case-insensitive)
        var exactPortMatch = devices.Find(d => d.PortName.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (exactPortMatch != null)
        {
            return exactPortMatch.PortName;
        }

        // 3. If target matches a TID exactly (case-sensitive or insensitive)
        var tidMatch = devices.Find(d => d.Tid.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (tidMatch != null)
        {
            Console.WriteLine($"[wio] Resolved Topology ID (TID) '{target}' to port: {tidMatch.PortName}");
            return tidMatch.PortName;
        }

        // 4. Default: Return target as is, hope the OS can open it (e.g. COM10, virtual port, etc.)
        return target;
    }
}
