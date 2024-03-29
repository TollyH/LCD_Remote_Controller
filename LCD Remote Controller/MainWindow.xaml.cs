using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;

namespace LCD_Remote_Controller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private const string configPath = "serial_config.json";

        private readonly SerialPort serialPort;

        private readonly Timer serialConsoleTimer = new(100);

        public MainWindow()
        {
            SerialConfig serialConfig;
            if (!File.Exists(configPath))
            {
                serialConfig = new SerialConfig("COM5");
                File.WriteAllText(configPath, JsonConvert.SerializeObject(serialConfig, Formatting.Indented));

                _ = MessageBox.Show(this, $"Please edit {configPath} with your serial port settings, then launch the app again.",
                    "First time config", MessageBoxButton.OK, MessageBoxImage.Information);

                Environment.Exit(1);
            }
            serialConfig = JsonConvert.DeserializeObject<SerialConfig>(File.ReadAllText(configPath));

            try
            {
                serialPort = new(
                    serialConfig.Device, serialConfig.BaudRate, serialConfig.ParityBits, serialConfig.DataBits, serialConfig.StopBits)
                {
                    NewLine = serialConfig.NewLine,
                    RtsEnable = true,
                    DtrEnable = true
                };
                serialPort.Open();

                serialPort.WriteLine("");
                serialPort.DiscardOutBuffer();
                serialPort.DiscardInBuffer();

                serialPort.WriteLine("#help");
                serialPort.WriteLine("#init 2 8");
            }
            catch (Exception exc)
            {
                _ = MessageBox.Show(this, $"There was an error communicating over the serial port. Please ensure that the settings in {configPath} are correct." +
                    $"\n\n{exc}", "Serial communication error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            InitializeComponent();
            SetDisplayOptions();

            serialConsoleTimer.Elapsed += SerialConsoleTimer_Elapsed;
            serialConsoleTimer.Start();
        }

        ~MainWindow()
        {
            Dispose();
        }

        public void Dispose()
        {
            serialPort.Dispose();
            GC.SuppressFinalize(this);
        }

        private void ClearDisplay_Click(object sender, RoutedEventArgs e)
        {
            serialPort.WriteLine("#clear");
        }

        private void ReturnHome_Click(object sender, RoutedEventArgs e)
        {
            serialPort.WriteLine("#home");
        }

        private void SetDisplayOptions()
        {
            string display = enableDisplay.IsChecked ?? false ? "1" : "0";
            string cursor = enableCursor.IsChecked ?? false ? "1" : "0";
            string blink = enableCursorBlinking.IsChecked ?? false ? "1" : "0";
            string backlight = enableBacklight.IsChecked ?? false ? "1" : "0";

            serialPort.WriteLine($"#set {display} {cursor} {blink}");
            serialPort.WriteLine($"#backlight {backlight}");
        }

        private void DisplayOptions_Click(object sender, RoutedEventArgs e)
        {
            SetDisplayOptions();
        }

        private void ScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement sendingButton)
            {
                serialPort.WriteLine($"#scroll {sendingButton.Tag}");
            }
        }

        private void WriteSend_Click(object sender, RoutedEventArgs e)
        {
            while (writeLine.Text[0] == '#')
            {
                // Text to write starts with '#'.
                // We need to write the '#' manually so that the controller doesn't interpret the text as a command.
                serialPort.WriteLine("#raw_tx 1 00100011");
                writeLine.Text = writeLine.Text[1..];
            }
            string[] lines = writeLine.Text.ReplaceLineEndings("\n").Split('\n', 2);
            if (lines.Length == 2)
            {
                serialPort.WriteLine(lines[0].TrimEnd('\n'));
                serialPort.WriteLine("#newline");
                serialPort.WriteLine(lines[1].TrimEnd('\n'));
            }
            else
            {
                serialPort.WriteLine(lines[0]);
            }
        }

        private void CustomCharInsert_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement sendingButton)
            {
                serialPort.WriteLine($"#write_custom {sendingButton.Tag}");
            }
        }

        private void CustomCharSave_Click(object sender, RoutedEventArgs e)
        {
            object? selectedChar = (customCharNumberPicker.SelectedItem as ComboBoxItem)?.Tag;

            string pixelArray = "";
            int i = 0;
            foreach (CheckBox pixel in charContainer.Children.OfType<CheckBox>())
            {
                pixelArray += pixel.IsChecked ?? false ? "1" : "0";
                if (++i % 5 == 0)
                {
                    pixelArray += ' ';
                }
            }

            serialPort.WriteLine($"#def_custom {selectedChar} {pixelArray}");
        }

        private void TransmitButton_Click(object sender, RoutedEventArgs e)
        {
            string rsPin = rs.IsChecked ?? false ? "1" : "0";

            string d0Pin = d0.IsChecked ?? false ? "1" : "0";
            string d1Pin = d1.IsChecked ?? false ? "1" : "0";
            string d2Pin = d2.IsChecked ?? false ? "1" : "0";
            string d3Pin = d3.IsChecked ?? false ? "1" : "0";
            string d4Pin = d4.IsChecked ?? false ? "1" : "0";
            string d5Pin = d5.IsChecked ?? false ? "1" : "0";
            string d6Pin = d6.IsChecked ?? false ? "1" : "0";
            string d7Pin = d7.IsChecked ?? false ? "1" : "0";

            serialPort.WriteLine($"#raw_tx {rsPin} {d7Pin}{d6Pin}{d5Pin}{d4Pin}{d3Pin}{d2Pin}{d1Pin}{d0Pin}");
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox senderCheckBox)
            {
                senderCheckBox.Background = senderCheckBox.IsChecked ?? false ? Brushes.Black : Brushes.White;
            }
        }

        private void MoveCursorButton_Click(object sender, RoutedEventArgs e)
        {
            object? selectedLine = (cursorLine.SelectedItem as ComboBoxItem)?.Content;
            object? selectedOffset = (cursorPosition.SelectedItem as ComboBoxItem)?.Content;

            serialPort.WriteLine($"#setpos {selectedLine} {selectedOffset}");
        }

        private void SerialConsoleTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            string newText = serialPort.ReadExisting();
            Dispatcher.Invoke(() =>
            {
                if (newText.Length > 0)
                {
                    serialConsole.Text += newText;
                    serialConsoleScroll.ScrollToBottom();
                }
            });
        }
    }
}
