using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using Wio.CLI;

namespace Wio.Engine;

public class SerialEngine
{
    private readonly WioOptions _options;
    private SerialPort? _serialPort;
    private Thread? _readThread;
    private volatile bool _isRunning;
    private readonly Action<byte[], int> _onDataReceived;
    private readonly Action<string> _onMessage;

    public bool IsConnected => _serialPort is { IsOpen: true };

    public SerialEngine(WioOptions options, Action<byte[], int> onDataReceived, Action<string> onMessage)
    {
        _options = options;
        _onDataReceived = onDataReceived;
        _onMessage = onMessage;
    }

    public void Start()
    {
        _isRunning = true;
        _readThread = new Thread(ConnectionLoop)
        {
            IsBackground = true,
            Name = "wio-SerialConnectionLoop"
        };
        _readThread.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        ClosePort();
        if (_readThread != null && _readThread.IsAlive)
        {
            _readThread.Join(1000);
        }
    }

    private void ConnectionLoop()
    {
        string portName = _options.Target ?? "";

        // If target is not specified, let's prompt or pick first/latest COM port
        if (string.IsNullOrEmpty(portName))
        {
            _onMessage("Error: No serial port target specified. Use 'wio -l' to list available ports.");
            return;
        }

        while (_isRunning)
        {
            _onMessage($"Connecting to {portName}...");

            if (TryOpenPort(portName))
            {
                _onMessage($"Connected to {portName} at {_options.BaudRate} baud.");
                
                // Read loop
                byte[] buffer = new byte[1024];
                while (_isRunning && IsConnected)
                {
                    try
                    {
                        int bytesRead = _serialPort!.BaseStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            _onDataReceived(buffer, bytesRead);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                        {
                            _onMessage("Connection lost.");
                            break;
                        }
                    }
                }

                ClosePort();
            }

            if (_options.NoReconnect)
            {
                _onMessage("Disconnecting (--no-reconnect).");
                break;
            }

            if (_isRunning)
            {
                _onMessage("Waiting for device to reappear...");
                // Poll for port appearance
                while (_isRunning)
                {
                    if (Array.Exists(SerialPort.GetPortNames(), name => name.Equals(portName, StringComparison.OrdinalIgnoreCase)))
                    {
                        break;
                    }
                    Thread.Sleep(1000);
                }
            }
        }
    }

    private bool TryOpenPort(string portName)
    {
        // Check if port exists before trying to open it
        if (!Array.Exists(SerialPort.GetPortNames(), name => name.Equals(portName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            _serialPort = new SerialPort(portName, _options.BaudRate, _options.Parity, _options.DataBits, _options.StopBits)
            {
                Handshake = _options.FlowControl,
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            
            _serialPort.Open();
            
            // Set DTR and RTS high by default
            _serialPort.DtrEnable = true;
            _serialPort.RtsEnable = true;
            
            return true;
        }
        catch (Exception ex)
        {
            _onMessage($"Error opening port {portName}: {ex.Message}");
            ClosePort();
            Thread.Sleep(2000); // Wait a bit before retrying
            return false;
        }
    }

    private void ClosePort()
    {
        try
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
        catch
        {
            // Suppress errors during cleanup
        }
    }

    public bool Write(byte[] data, int offset, int count)
    {
        var port = _serialPort;
        if (port != null && port.IsOpen)
        {
            try
            {
                port.Write(data, offset, count);
                return true;
            }
            catch
            {
                // Error writing
            }
        }
        return false;
    }

    public void PulseDtr(int durationMs)
    {
        var port = _serialPort;
        if (port != null && port.IsOpen)
        {
            try
            {
                port.DtrEnable = false;
                Thread.Sleep(durationMs);
                port.DtrEnable = true;
            }
            catch (Exception ex)
            {
                _onMessage($"Failed to pulse DTR line: {ex.Message}");
            }
        }
    }

    public void PulseRts(int durationMs)
    {
        var port = _serialPort;
        if (port != null && port.IsOpen)
        {
            try
            {
                port.RtsEnable = false;
                Thread.Sleep(durationMs);
                port.RtsEnable = true;
            }
            catch (Exception ex)
            {
                _onMessage($"Failed to pulse RTS line: {ex.Message}");
            }
        }
    }

    public void ToggleDtr()
    {
        var port = _serialPort;
        if (port != null && port.IsOpen)
        {
            try
            {
                port.DtrEnable = !port.DtrEnable;
                _onMessage($"DTR toggled to: {port.DtrEnable}");
            }
            catch (Exception ex)
            {
                _onMessage($"Failed to toggle DTR line: {ex.Message}");
            }
        }
    }

    public void ToggleRts()
    {
        var port = _serialPort;
        if (port != null && port.IsOpen)
        {
            try
            {
                port.RtsEnable = !port.RtsEnable;
                _onMessage($"RTS toggled to: {port.RtsEnable}");
            }
            catch (Exception ex)
            {
                _onMessage($"Failed to toggle RTS line: {ex.Message}");
            }
        }
    }
}
