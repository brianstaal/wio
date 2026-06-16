using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Wio.Engine;

public class SerialDeviceDetails
{
    public string PortName { get; set; } = "";
    public string Tid { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Description { get; set; } = "";
    public string PnpDeviceId { get; set; } = "";
}

public static class DeviceMonitor
{
    public static List<SerialDeviceDetails> GetSerialDevices()
    {
        var devices = new List<SerialDeviceDetails>();

        try
        {
            // 1. Get all active serial ports from SERIALCOMM mapping
            var activePorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var serialCommKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
            {
                if (serialCommKey != null)
                {
                    foreach (var valueName in serialCommKey.GetValueNames())
                    {
                        string? port = serialCommKey.GetValue(valueName)?.ToString();
                        if (!string.IsNullOrEmpty(port))
                        {
                            activePorts.Add(port);
                        }
                    }
                }
            }

            // Fallback: If SERIALCOMM is empty, use System.IO.Ports.SerialPort
            if (activePorts.Count == 0)
            {
                foreach (var port in SerialPort.GetPortNames())
                {
                    activePorts.Add(port);
                }
            }

            // 2. Query the Class GUID registry key to find matching COM ports with metadata
            string classKeyPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e978-e325-11ce-bfc1-08002be10318}";
            using (var classKey = Registry.LocalMachine.OpenSubKey(classKeyPath))
            {
                if (classKey != null)
                {
                    foreach (var subkeyName in classKey.GetSubKeyNames())
                    {
                        // Ignore non-numeric subkeys
                        if (!int.TryParse(subkeyName, out _)) continue;

                        using (var portSubkey = classKey.OpenSubKey(subkeyName))
                        {
                            if (portSubkey == null) continue;

                            string? portName = portSubkey.GetValue("PortName")?.ToString() ?? 
                                               portSubkey.GetValue("PortNameEntry")?.ToString();
                            
                            if (string.IsNullOrEmpty(portName)) continue;

                            // Only include active ports (which are plugged in right now)
                            if (!activePorts.Contains(portName)) continue;

                            string description = portSubkey.GetValue("DriverDesc")?.ToString() ?? "Serial Device";
                            string provider = portSubkey.GetValue("ProviderName")?.ToString() ?? "Unknown";
                            string deviceInstance = portSubkey.GetValue("DeviceInstance")?.ToString() ?? portName;

                            string tid = CalculateTid(deviceInstance);

                            devices.Add(new SerialDeviceDetails
                            {
                                PortName = portName,
                                Tid = tid,
                                Manufacturer = provider,
                                Description = description,
                                PnpDeviceId = deviceInstance
                            });

                            // Remove from activePorts set so we don't duplicate it in step 3
                            activePorts.Remove(portName);
                        }
                    }
                }
            }

            // 3. Add fallbacks for any remaining active ports
            foreach (var remainingPort in activePorts)
            {
                devices.Add(new SerialDeviceDetails
                {
                    PortName = remainingPort,
                    Tid = CalculateTid(remainingPort),
                    Manufacturer = "Unknown",
                    Description = "Serial Port",
                    PnpDeviceId = remainingPort
                });
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to query registry for serial devices: {ex.Message}");
        }

        // Sort by port name (numeric sorting, e.g. COM2 before COM10)
        devices.Sort((a, b) =>
        {
            int aNum = ExtractNumber(a.PortName);
            int bNum = ExtractNumber(b.PortName);
            if (aNum != bNum)
            {
                return aNum.CompareTo(bNum);
            }
            return string.Compare(a.PortName, b.PortName, StringComparison.OrdinalIgnoreCase);
        });

        return devices;
    }

    private static int ExtractNumber(string portName)
    {
        var match = Regex.Match(portName, @"\d+");
        return match.Success && int.TryParse(match.Value, out int num) ? num : int.MaxValue;
    }

    public static string CalculateTid(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "0000";
        }

        // Clean up input to normalize
        string normalized = input.Trim().Replace('\\', '/');

        // djb2 hash
        uint hash = 5381;
        foreach (char c in normalized)
        {
            hash = ((hash << 5) + hash) + (byte)c;
        }

        // base62 encode (exactly 4 chars)
        const string base62Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        char[] output = new char[4];
        for (int i = 0; i < 4; ++i)
        {
            output[i] = base62Chars[(int)(hash % 62)];
            hash /= 62;
        }

        return new string(output);
    }

    public static void PrintDeviceList()
    {
        var devices = GetSerialDevices();

        Console.WriteLine("\nAvailable serial devices:\n");
        Console.WriteLine($"{"Port",-12} {"Tid",-5} {"Manufacturer",-25} {"Description"}");
        Console.WriteLine($"{new string('-', 12)} {new string('-', 5)} {new string('-', 25)} {new string('-', 35)}");

        if (devices.Count == 0)
        {
            Console.WriteLine("No serial devices found.");
            return;
        }

        foreach (var device in devices)
        {
            // Truncate manufacturer if too long
            string manufacturer = device.Manufacturer.Length > 24 
                ? device.Manufacturer[..21] + "..." 
                : device.Manufacturer;

            Console.WriteLine($"{device.PortName,-12} {device.Tid,-5} {manufacturer,-25} {device.Description}");
        }
        Console.WriteLine();
    }
}
