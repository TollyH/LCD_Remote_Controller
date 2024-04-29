using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;

namespace LCD_Remote_Controller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private const string configPath = "process_config.json";
        private const string consoleReadyPrompt = "\n> ";

        private readonly Process process;
        private readonly ProcessConfig processConfig;

        private readonly CancellationTokenSource stdoutReadCancel = new();

        private int stdoutReadStartIndex = 0;

        public MainWindow()
        {
            if (!File.Exists(configPath))
            {
                processConfig = new ProcessConfig("LCDSimulator.GUI.exe");
                File.WriteAllText(configPath, JsonConvert.SerializeObject(processConfig, Formatting.Indented));

                _ = MessageBox.Show(this, $"Please edit {configPath} with your process settings, then launch the app again.",
                    "First time config", MessageBoxButton.OK, MessageBoxImage.Information);

                Environment.Exit(1);
            }
            processConfig = JsonConvert.DeserializeObject<ProcessConfig>(File.ReadAllText(configPath));

            try
            {
                process = Process.Start(new ProcessStartInfo(processConfig.Path)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }) ?? throw new Exception();

                process.StandardInput.AutoFlush = true;

                process.StandardInput.WriteLine("");

                process.StandardInput.WriteLine("#help");
                if (!Environment.GetCommandLineArgs().Contains("--no-init"))
                {
                    process.StandardInput.WriteLine("#power 1");
                    process.StandardInput.WriteLine($"#set_size {processConfig.LCDHeight} {processConfig.LCDWidth}");
                    process.StandardInput.WriteLine("#init 2 8");
                }
            }
            catch (Exception exc)
            {
                _ = MessageBox.Show(this, $"There was an error communicating with the process. Please ensure that the settings in {configPath} are correct." +
                    $"\n\n{exc}", "Process communication error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            InitializeComponent();
            SetDisplayOptions();

            writeBox.Width = processConfig.LCDWidth * 9 + 5;
            writeBox.Height = processConfig.LCDHeight * 20 + 5;

            for (int i = 1; i <= processConfig.LCDHeight; i++)
            {
                cursorLine.Items.Add(new ComboBoxItem()
                {
                    Content = i.ToString()
                });
            }

            for (int i = 0; i < processConfig.LCDWidth; i++)
            {
                cursorPosition.Items.Add(new ComboBoxItem()
                {
                    Content = i.ToString()
                });
            }

            _ = StartConsoleUpdateLoop();
        }

        ~MainWindow()
        {
            Dispose();
        }

        public void Dispose()
        {
            process.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<string> ReadProcessOutput()
        {
            try
            {
                char[] buffer = new char[1024];
                int numRead = await process.StandardOutput.ReadAsync(new Memory<char>(buffer, 0, 1024), stdoutReadCancel.Token);
                return new string(buffer, 0, numRead);
            }
            catch (OperationCanceledException) { }

            return "";
        }

        public void StartProcessInputCapture()
        {
            stdoutReadStartIndex = processConsole.Text.Length;
        }

        public async Task<string> EndProcessInputCapture(bool waitForCommandFinish)
        {
            string newText = processConsole.Text[stdoutReadStartIndex..];
            while (waitForCommandFinish && !newText.EndsWith(consoleReadyPrompt, StringComparison.Ordinal))
            {
                newText += processConsole.Text[(stdoutReadStartIndex + newText.Length)..];
                await Task.Delay(50);
            }

            return newText;
        }

        public async Task StartConsoleUpdateLoop()
        {
            while (!stdoutReadCancel.IsCancellationRequested)
            {
                await ReadProcessDataToConsole();
            }
        }

        public async Task ReadProcessDataToConsole()
        {
            AddTextToConsole(await ReadProcessOutput());
        }

        public static string GetCommandOutput(string processText)
        {
            string[] lines = processText.ReplaceLineEndings("\n").Replace(consoleReadyPrompt, "").Trim('\n').Split('\n');
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
                processConsole.Text += text;
                processConsoleScroll.ScrollToBottom();
            }
        }

        private void ClearDisplay_Click(object sender, RoutedEventArgs e)
        {
            process.StandardInput.WriteLine("#clear");
        }

        private void ReturnHome_Click(object sender, RoutedEventArgs e)
        {
            process.StandardInput.WriteLine("#home");
        }

        private void SetDisplayOptions()
        {
            string display = enableDisplay.IsChecked ?? false ? "1" : "0";
            string cursor = enableCursor.IsChecked ?? false ? "1" : "0";
            string blink = enableCursorBlinking.IsChecked ?? false ? "1" : "0";
            string backlight = enableBacklight.IsChecked ?? false ? "1" : "0";

            process.StandardInput.WriteLine($"#set {display} {cursor} {blink}");
            process.StandardInput.WriteLine($"#backlight {backlight}");
        }

        private void DisplayOptions_Click(object sender, RoutedEventArgs e)
        {
            SetDisplayOptions();
        }

        private void ScrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement sendingButton)
            {
                process.StandardInput.WriteLine($"#scroll {sendingButton.Tag}");
            }
        }

        private void TextWrite_Click(object sender, RoutedEventArgs e)
        {
            string[] lines = writeBox.Text.ReplaceLineEndings("\n").Split('\n');
            foreach (string line in lines)
            {
                int skip = 0;
                while (line.StartsWith('#'))
                {
                    // Leading '#' should be written manually so that the controller doesn't interpret the text as a command.
                    process.StandardInput.WriteLine("#raw_tx 1 00100011");
                    skip++;
                }
                process.StandardInput.WriteLine(line[skip..].TrimEnd('\n'));
                if (line.Length != processConfig.LCDWidth)
                {
                    process.StandardInput.WriteLine("#newline");
                }
            }
        }

        // ReSharper disable once AsyncVoidMethod - WPF callback cannot be async Task
        private async void TextRead_Click(object sender, RoutedEventArgs e)
        {
            StartProcessInputCapture();
            await process.StandardInput.WriteLineAsync("#read");
            string rawOutput = await EndProcessInputCapture(true);

            writeBox.Text = GetCommandOutput(rawOutput);
        }

        private void CustomCharInsert_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement sendingButton)
            {
                process.StandardInput.WriteLine($"#write_custom {sendingButton.Tag}");
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

            process.StandardInput.WriteLine($"#def_custom {selectedChar} {pixelArray}");
        }

        // ReSharper disable once AsyncVoidMethod - WPF callback cannot be async Task
        private async void CustomCharLoad_Click(object sender, RoutedEventArgs e)
        {
            object? selectedChar = (customCharNumberPicker.SelectedItem as ComboBoxItem)?.Tag;

            StartProcessInputCapture();
            await process.StandardInput.WriteLineAsync($"#read_custom {selectedChar}");
            string rawOutput = await EndProcessInputCapture(true);

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

            process.StandardInput.WriteLine($"#raw_tx {rsPin} {d7Pin}{d6Pin}{d5Pin}{d4Pin}{d3Pin}{d2Pin}{d1Pin}{d0Pin}");
        }

        // ReSharper disable once AsyncVoidMethod - WPF callback cannot be async Task
        private async void ReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            string rsPin = rs.IsChecked ?? false ? "1" : "0";

            StartProcessInputCapture();
            await process.StandardInput.WriteLineAsync($"#raw_rx {rsPin}");
            string rawOutput = await EndProcessInputCapture(true);

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

            process.StandardInput.WriteLine($"#setpos {selectedLine} {selectedOffset}");
        }

        // ReSharper disable once AsyncVoidMethod - WPF callback cannot be async Task
        private async void GetCursorButton_Click(object sender, RoutedEventArgs e)
        {
            StartProcessInputCapture();
            await process.StandardInput.WriteLineAsync("#getpos");
            string rawOutput = await EndProcessInputCapture(true);

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

        private void ProcessSendButton_Click(object sender, RoutedEventArgs e)
        {
            process.StandardInput.WriteLine(processConsoleInput.Text);
        }

        private void processConsoleInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                process.StandardInput.WriteLine(processConsoleInput.Text);
            }
        }
    }
}
