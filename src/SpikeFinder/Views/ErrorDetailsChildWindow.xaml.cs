using MahApps.Metro.SimpleChildWindow;
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Windows;

namespace SpikeFinder.Views
{
    /// <summary>
    /// Interaction logic for ErrorDetailsChildWindow.xaml
    /// </summary>
    public partial class ErrorDetailsChildWindow : ChildWindow
    {
        public ErrorDetailsChildWindow(Exception ex)
        {
            InitializeComponent();

            Details = CalculateDetails(ex);
            DataContext = this;
        }

        public string Details { get; }

        private string CalculateDetails(Exception ex)
        {
            var sb = new StringBuilder();

            for (Exception? ex1 = ex; ex1 != null; ex1 = ex1.InnerException)
            {
                PrintException(ex1, sb);
            }

            return sb.ToString().TrimEnd();
        }
        private void PrintException(Exception ex, StringBuilder sb)
        {
            if (ex == null)
            {
                throw new ArgumentNullException(nameof(ex));
            }

            sb.AppendLine("===================================").AppendLine();

            if (ex.Message != null && ex.Message.Length != 0)
            {
                sb.Append(ex.Message).AppendLine().AppendLine();
            }

            const string AdvancedInfo = "AdvancedInformation.";
            var hasAdvancedInfo = false;

            foreach (DictionaryEntry x in ex.Data)
            {
                if (x.Key is string key && $"{x.Value}".Length > 0 && string.Compare((string)x.Key, 0, AdvancedInfo, 0, AdvancedInfo.Length, false, CultureInfo.CurrentCulture) == 0)
                {
                    if (!hasAdvancedInfo)
                    {
                        sb.AppendLine("------------------------------");
                    }

                    sb.AppendLine($"{key.Substring(AdvancedInfo.Length)}: {x.Value}");
                    hasAdvancedInfo = true;
                }
            }

            if (hasAdvancedInfo)
            {
                sb.AppendLine().AppendLine();
            }

            if (ex.StackTrace?.Length > 0)
            {
                sb.AppendLine("------------------------------").AppendLine("Program Location:").AppendLine().AppendLine(ex.StackTrace).AppendLine();
            }
        }

        private void OK_Clicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
