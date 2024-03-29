using Microsoft.VisualBasic;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LCD_Remote_Controller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public readonly string RootDomain;
        public readonly HttpClient WebConnect = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            RootDomain = "http://" + Interaction.InputBox("Enter domain to connect to", "Domain entry");
            if (RootDomain == "http://")
            {
                MessageBox.Show("You must enter a domain", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
            WebConnect.PostAsync(RootDomain + "/clear", null);
            System.Threading.Thread.Sleep(50);
            WebConnect.PostAsync(RootDomain + "/display_set?d=0&c=0&b=0", null);
            System.Threading.Thread.Sleep(50);
            WebConnect.PostAsync(RootDomain + "/backlight?var=0", null);
        }

        private void ClearDisplay_Click(object sender, RoutedEventArgs e)
        {
            WebConnect.PostAsync(RootDomain + "/clear", null);
        }

        private void ReturnHome_Click(object sender, RoutedEventArgs e)
        {
            WebConnect.PostAsync(RootDomain + "/home", null);
        }

        private void DisplayOptions_Click(object sender, RoutedEventArgs e)
        {
            WebConnect.PostAsync(RootDomain + string.Format("/display_set?d={0}&c={1}&b={2}",
                EnableDisplay.IsChecked.Value ? "1" : "0", EnableCursor.IsChecked.Value ? "1" : "0", EnableCursorBlinking.IsChecked.Value ? "1" : "0"), null);
            WebConnect.PostAsync(RootDomain + "/backlight?var=" + (EnableBacklight.IsChecked.Value ? "1" : "0"), null);
        }

        private void ScrollButton_Click(object sender, RoutedEventArgs e)
        {
            Button SendingButton = (Button)sender;
            WebConnect.PostAsync(RootDomain + string.Format("/scroll?cs={0}&lr={1}",
                SendingButton.Name.Contains("Screen") ? "1" : "0", SendingButton.Name.Contains("Right") ? "1" : "0"), null);
        }

        private void WriteSend_Click(object sender, RoutedEventArgs e)
        {
            WebConnect.PostAsync(RootDomain + "/clear", null);
            System.Threading.Thread.Sleep(50);
            WebConnect.PostAsync(RootDomain + "/write?message=" + HttpUtility.UrlEncode(WriteLineOne.Text.PadRight(16) + WriteLineTwo.Text), null);
        }

        private void CustomCharInsert_Click(object sender, RoutedEventArgs e)
        {
            Match RegexResult = Regex.Match(((Button)sender).Name, "CustomCharLine([1-2])Char([1-8])");
            if (RegexResult.Groups[1].Value == "1")
            {
                if (WriteLineOne.Text.Length < 16)
                {
                    WriteLineOne.Text += Convert.ToChar(0xE000 + int.Parse(RegexResult.Groups[2].Value) - 1);
                }
            }
            else if (RegexResult.Groups[1].Value == "2")
            {
                if (WriteLineTwo.Text.Length < 16)
                {
                    WriteLineTwo.Text += Convert.ToChar(0xE000 + int.Parse(RegexResult.Groups[2].Value) - 1);
                }
            }
        }

        private void CustomCharSave_Click(object sender, RoutedEventArgs e)
        {
            string PixelArray = "";
            foreach (CheckBox pixel in CharContainer.Children.OfType<CheckBox>())
            {
                PixelArray += Convert.ToInt32(pixel.IsChecked.Value);
            }
            WebConnect.PostAsync(RootDomain + string.Format("/custom_char?pixel_array={0}&char_number={1}", PixelArray, CustomCharNumberPicker.Text), null);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox SenderCheckBox = (CheckBox)sender;
            SenderCheckBox.Background = SenderCheckBox.IsChecked.Value ? Brushes.Black : Brushes.White;
        }

        private void AdvancedButton_Click(object sender, RoutedEventArgs e)
        {
            new AdvancedWindow(this).Show();
        }
    }
}
