using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;

namespace RS485ChipProgrammer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    // Properties bound to the UI
        private string _portName;
        public string PortName
        {
            get => _portName;
            set => this.RaiseAndSetIfChanged(ref _portName, value);
        }

        private int _baudRate = 9600;
        public int BaudRate
        {
            get => _baudRate;
            set => this.RaiseAndSetIfChanged(ref _baudRate, value);
        }

        private string _commandToSend = "WRITE_DATA:12345";
        public string CommandToSend
        {
            get => _commandToSend;
            set => this.RaiseAndSetIfChanged(ref _commandToSend, value);
        }

        private string _logText = "Application ready.\n";
        public string LogText
        {
            get => _logText;
            set => this.RaiseAndSetIfChanged(ref _logText, value);
        }

        private string _connectButtonText = "Connect";
        public string ConnectButtonText
        {
            get => _connectButtonText;
            set => this.RaiseAndSetIfChanged(ref _connectButtonText, value);
        }

        private bool _isConnectButtonEnabled = true;
        public bool IsConnectButtonEnabled
        {
            get => _isConnectButtonEnabled;
            set => this.RaiseAndSetIfChanged(ref _isConnectButtonEnabled, value);
        }

        private bool _isSendCommandEnabled = false;
        public bool IsSendCommandEnabled
        {
            get => _isSendCommandEnabled;
            set => this.RaiseAndSetIfChanged(ref _isSendCommandEnabled, value);
        }

        // Collections for ComboBoxes
        public ObservableCollection<string> AvailablePorts { get; }
        public ObservableCollection<int> AvailableBaudRates { get; } = new ObservableCollection<int> { 9600, 19200, 38400, 57600, 115200 };

        // Commands for UI buttons
        public ICommand ConnectCommand { get; }
        public ICommand SendCommand { get; }

        private SerialPort _serialPort;
        private const int READ_TIMEOUT = 2000;

        public MainWindowViewModel()
        {
            // Initialize collections and commands
            AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames().OrderBy(p => p));
            PortName = AvailablePorts.FirstOrDefault();
            
            // Set up commands using ReactiveCommand
            ConnectCommand = ReactiveCommand.CreateFromTask(ToggleConnectionAsync,
                this.WhenAnyValue(x => x.IsConnectButtonEnabled));

            SendCommand = ReactiveCommand.CreateFromTask(SendCommandAsync,
                this.WhenAnyValue(x => x.IsSendCommandEnabled));
        }

        private async Task ToggleConnectionAsync()
        {
            // Toggle connect/disconnect functionality
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                await ConnectAsync();
            }
            else
            {
                Disconnect();
            }
        }

        private async Task ConnectAsync()
        {
            if (string.IsNullOrEmpty(PortName))
            {
                AddLog("ERROR: Please select a COM port.");
                return;
            }

            try
            {
                IsConnectButtonEnabled = false;
                ConnectButtonText = "Connecting...";

                // Create and configure the SerialPort object
                _serialPort = new SerialPort(PortName, BaudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = READ_TIMEOUT,
                    WriteTimeout = 500,
                    Handshake = Handshake.None
                };

                // Use Task.Run to open the port on a background thread
                await Task.Run(() => _serialPort.Open());

                IsSendCommandEnabled = true;
                ConnectButtonText = "Disconnect";
                AddLog($"Successfully connected to {PortName} at {BaudRate} baud.");
            }
            catch (Exception ex)
            {
                AddLog($"ERROR: Failed to connect. Details: {ex.Message}");
                Disconnect();
            }
            finally
            {
                IsConnectButtonEnabled = true;
            }
        }

        private void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                AddLog("Serial port closed.");
            }
            IsSendCommandEnabled = false;
            ConnectButtonText = "Connect";
        }

        private async Task SendCommandAsync()
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                AddLog("ERROR: Not connected. Please connect to a port first.");
                return;
            }

            try
            {
                IsSendCommandEnabled = false;
                AddLog($"\nSending command: '{CommandToSend}'...");

                // Use Task.Run to perform the serial communication on a background thread
                string response = await Task.Run(() =>
                {
                    // For RS485, switch to transmit mode
                    _serialPort.RtsEnable = true;
                    Thread.Sleep(50); // Small delay to ensure the transmitter is active

                    // Write the command
                    _serialPort.WriteLine(CommandToSend);
                    Thread.Sleep(50); // Small delay for transmission

                    // Switch back to receive mode
                    _serialPort.RtsEnable = false;

                    // Read the response
                    return _serialPort.ReadLine();
                });
                
                AddLog($"Received response: '{response}'");
            }
            catch (TimeoutException)
            {
                AddLog($"ERROR: The operation timed out. No response received within {READ_TIMEOUT} ms.");
            }
            catch (Exception ex)
            {
                AddLog($"An error occurred while sending command: {ex.Message}");
            }
            finally
            {
                IsSendCommandEnabled = true;
            }
        }

        private void AddLog(string message)
        {
            // Use Dispatcher.UIThread.InvokeAsync to update the UI from a background thread.
            // This is crucial for cross-thread operations.
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                LogText += $"{DateTime.Now:HH:mm:ss} - {message}\n";
            });
        }
}