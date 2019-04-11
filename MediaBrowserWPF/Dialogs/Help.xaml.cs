using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für Help.xaml
    /// </summary>
    public partial class Help : Window
    {
        public Help()
        {
            InitializeComponent();
        }

        private DispatcherTimer closeTimer = new DispatcherTimer();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {     
            Window mainWindow = Application.Current.MainWindow;
            this.Left = mainWindow.Left + (mainWindow.Width - this.ActualWidth) / 2;
            this.Top = mainWindow.Top + (mainWindow.Height - this.ActualHeight) / 2;

            this.closeTimer.Interval = new TimeSpan(0, 0, 0, 3, 0);
            this.closeTimer.Tick += new EventHandler(closeTimer_Tick);
            this.closeTimer.Start();
        }

        void closeTimer_Tick(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
