using System.Web;
using System.Windows;

namespace LCD_Remote_Controller
{
    /// <summary>
    /// Interaction logic for AdvancedWindow.xaml
    /// </summary>
    public partial class AdvancedWindow : Window
    {
        private readonly MainWindow PassingWindow;

        public AdvancedWindow(MainWindow passingWindow)
        {
            PassingWindow = passingWindow;
            InitializeComponent();
        }

        private void TransmitButton_Click(object sender, RoutedEventArgs e)
        {
            PassingWindow.WebConnect.PostAsync(PassingWindow.RootDomain + string.Format("/raw_write?rs={0}&d0={1}&d1={2}&d2={3}&d3={4}&d4={5}&d5={6}&d6={7}&d7={8}",
                rs.IsChecked.Value ? "1" : "0", d0.IsChecked.Value ? "1" : "0", d1.IsChecked.Value ? "1" : "0", d2.IsChecked.Value ? "1" : "0", d3.IsChecked.Value ? "1" : "0",
                d4.IsChecked.Value ? "1" : "0", d5.IsChecked.Value ? "1" : "0", d6.IsChecked.Value ? "1" : "0", d7.IsChecked.Value ? "1" : "0"), null);
        }

        private void WriteSend_Click(object sender, RoutedEventArgs e)
        {
            PassingWindow.WebConnect.PostAsync(PassingWindow.RootDomain + "/write?dont_go_home_first&message=" + HttpUtility.UrlEncode(WriteLine.Text), null);
        }
    }
}
