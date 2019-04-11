using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MediaBrowserWPF.UserControls.ThumbListContainer
{
    /// <summary>
    /// Interaktionslogik für AdjustMediaDateDialog.xaml
    /// </summary>
    public partial class AdjustMediaDateDialog : Window
    {
        public AdjustMediaDateDialog()
        {
            InitializeComponent();
        }

        public AdjustMediaDateDialog(string question, string defaultAnswer = "")
        {
            InitializeComponent();
            lblQuestion.Content = question;
            txtAnswer.Text = defaultAnswer;
        }

        public string Answer
        {
            get { return txtAnswer.Text; }
        }

        public DateTime AnswerDate
        {
            get { return DateTime.ParseExact(txtAnswer.Text, "dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture); }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            DateTime date;
            if (DateTime.TryParseExact(txtAnswer.Text, "dd.MM.yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date))
            {
                this.DialogResult = true;
            }else
            {
                Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, txtAnswer.Text,
                                  "Falsches Format", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
