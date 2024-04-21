using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string consoleReadyPrompt = "\n> ";

        private readonly SerialPort serialPort;
        private readonly SerialConfig serialConfig;

        private readonly Timer serialConsoleTimer = new(100);

        public MainWindow()
        {
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
                serialPort = new SerialPort(
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
                if (!Environment.GetCommandLineArgs().Contains("--no-init"))
                {
                    serialPort.WriteLine($"#set_size {serialConfig.LCDHeight} {serialConfig.LCDWidth}");
                    serialPort.WriteLine("#init 2 8");
                }
            }
            catch (Exception exc)
            {
                _ = MessageBox.Show(this, $"There was an error communicating over the serial port. Please ensure that the settings in {configPath} are correct." +
                    $"\n\n{exc}", "Serial communication error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            InitializeComponent();
            SetDisplayOptions();

            writeBox.Width = serialConfig.LCDWidth * 9 + 5;
            writeBox.Height = serialConfig.LCDHeight * 20 + 5;

            for (int i = 1; i <= serialConfig.LCDHeight; i++)
            {
                cursorLine.Items.Add(new ComboBoxItem()
                {
                    Content = i.ToString()
                });
            }

            for (int i = 0; i < serialConfig.LCDWidth; i++)
            {
                cursorPosition.Items.Add(new ComboBoxItem()
                {
                    Content = i.ToString()
                });
            }

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

        public void StartSerialInputCapture()
        {
            serialConsoleTimer.Stop();
            ReadSerialDataToConsole();
        }

        public string EndSerialInputCapture(bool waitForCommandFinish)
        {
            string newText = "";
            do
            {
                newText += serialPort.ReadExisting();
            } while (waitForCommandFinish && !newText.EndsWith(consoleReadyPrompt, StringComparison.Ordinal));

            AddTextToConsole(newText);
            serialConsoleTimer.Start();

            return newText;
        }

        public void ReadSerialDataToConsole()
        {
            AddTextToConsole(serialPort.ReadExisting());
        }

        public static string GetCommandOutput(string serialText)
        {
            string[] lines = serialText.ReplaceLineEndings("\n").Replace(consoleReadyPrompt, "").Trim('\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimEnd();
            }

            return lines.Length == 0 ? "" : string.Join('\n', lines[1..]);
        }

        private void AddTextToConsole(string text)
        {
            if (text.Length > 0)
            {
                serialConsole.Text += text;
                serialConsoleScroll.ScrollToBottom();
            }
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

        private void TextWrite_Click(object sender, RoutedEventArgs e)
        {
            string[] lines = writeBox.Text.ReplaceLineEndings("\n").Split('\n');
            foreach (string line in lines)
            {
                // '#' should be written manually so that the controller doesn't interpret the text as a command.
                serialPort.WriteLine(line.TrimEnd('\n').Replace("#", "#raw_tx 1 00100011\n"));
                if (line.Length != serialConfig.LCDWidth)
                {
                    serialPort.WriteLine("#newline");
                }
            }
        }

        private void TextRead_Click(object sender, RoutedEventArgs e)
        {
            StartSerialInputCapture();
            serialPort.WriteLine("#read");
            string rawOutput = EndSerialInputCapture(true);

            writeBox.Text = GetCommandOutput(rawOutput);
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

        private void CustomCharLoad_Click(object sender, RoutedEventArgs e)
        {
            object? selectedChar = (customCharNumberPicker.SelectedItem as ComboBoxItem)?.Tag;

            StartSerialInputCapture();
            serialPort.WriteLine($"#read_custom {selectedChar}");
            string rawOutput = EndSerialInputCapture(true);

            string output = GetCommandOutput(rawOutput);
            if (output.Length == 0)
            {
                return;
            }

            string[] lines = output.Split(' ');
            if (lines.Length < 8)
            {
                return;
            }

            CheckBox[] pixelCheckBoxes = charContainer.Children.OfType<CheckBox>().ToArray();
            for (int i = 0; i < 8; i++)
            {
                string line = lines[i];
                if (line.Length < 5)
                {
                    continue;
                }
                if (line.Length > 5)
                {
                    // Only consider the lowest 5 binary digits if there are more
                    line = string.Concat(line.TakeLast(5));
                }
                for (int j = 0; j < 5; j++)
                {
                    pixelCheckBoxes[i * 5 + j].IsChecked = line[j] == '1';
                }
            }
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

        private void ReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            string rsPin = rs.IsChecked ?? false ? "1" : "0";

            StartSerialInputCapture();
            serialPort.WriteLine($"#raw_rx {rsPin}");
            string rawOutput = EndSerialInputCapture(true);

            string output = GetCommandOutput(rawOutput);
            if (output.Length == 0)
            {
                return;
            }
            // Binary digits are the first component of the output
            output = output.Split(' ')[0];
            if (output.Length < 8)
            {
                return;
            }

            d0.IsChecked = output[7] == '1';
            d1.IsChecked = output[6] == '1';
            d2.IsChecked = output[5] == '1';
            d3.IsChecked = output[4] == '1';
            d4.IsChecked = output[3] == '1';
            d5.IsChecked = output[2] == '1';
            d6.IsChecked = output[1] == '1';
            d7.IsChecked = output[0] == '1';
        }

        private void CustomPixel_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox senderCheckBox)
            {
                senderCheckBox.Background = Brushes.Black;
            }
        }

        private void CustomPixel_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox senderCheckBox)
            {
                senderCheckBox.Background = Brushes.White;
            }
        }

        private void MoveCursorButton_Click(object sender, RoutedEventArgs e)
        {
            object? selectedLine = (cursorLine.SelectedItem as ComboBoxItem)?.Content;
            object? selectedOffset = (cursorPosition.SelectedItem as ComboBoxItem)?.Content;

            serialPort.WriteLine($"#setpos {selectedLine} {selectedOffset}");
        }

        private void GetCursorButton_Click(object sender, RoutedEventArgs e)
        {
            StartSerialInputCapture();
            serialPort.WriteLine("#getpos");
            string rawOutput = EndSerialInputCapture(true);

            string output = GetCommandOutput(rawOutput);
            Match linePosMatch = Regex.Match(output, "line: ([0-9]+), offset: ([0-9]+)");
            if (!linePosMatch.Success)
            {
                return;
            }

            if (int.TryParse(linePosMatch.Groups[1].Value, out int line))
            {
                cursorLine.SelectedIndex = line - 1;
            }
            if (int.TryParse(linePosMatch.Groups[2].Value, out int offset))
            {
                cursorPosition.SelectedIndex = offset;
            }
        }

        private void SerialConsoleTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(ReadSerialDataToConsole);
        }

        private void SerialSendButton_Click(object sender, RoutedEventArgs e)
        {
            serialPort.WriteLine(serialConsoleInput.Text);
        }
    }
}
