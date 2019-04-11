using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using MediaBrowser4;
using System.IO;
using MediaBrowser4.Utilities;
using System.Windows.Markup;
using System.Globalization;

namespace MediaBrowserWPF
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            SplashScreen ss = new SplashScreen("Images\\Splash.jpg");
            ss.Show(true, true);

            string dbPath = null;
            string initPath = null;
            string initFolder = null;
            if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            {
                dbPath = e.Args[0];

                if (e.Args.Length == 2 && Directory.Exists(e.Args[1]))
                {
                    initFolder = e.Args[1];
                }
            }
            else if (e.Args.Length == 1 && Directory.Exists(e.Args[0]))
            {
                initPath = e.Args[0];
            }
            else
            {
                foreach (string part in MediaBrowserWPF.Properties.Settings.Default.DBPath.Split(';'))
                {
                    if (File.Exists(part))
                    {
                        dbPath = part;
                        break;
                    }
                }
            }

            MediaBrowserContext.Init(dbPath);

            MediaBrowser4.MediaBrowserContext.SetDirectShowExtensions
                = MediaBrowserWPF.Properties.Settings.Default.DirectShowExtensions;

            MediaBrowser4.MediaBrowserContext.SetRGBExtensions
                = MediaBrowserWPF.Properties.Settings.Default.RGBExtensions;

            MediaBrowser4.MediaBrowserContext.SetAudioExtraFiles
                = MediaBrowserWPF.Properties.Settings.Default.AudioExtraFiles;

            MainWindow mainWindow = new MainWindow();
            mainWindow.InitPath = initPath;
            mainWindow.InitFolder = initFolder;
            mainWindow.Show();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    XmlLanguage.GetLanguage(
                    CultureInfo.CurrentCulture.IetfLanguageTag)));
            base.OnStartup(e);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            MediaBrowserWPF.Properties.Settings.Default.Save();
            MediaBrowserContext.SaveDBProperties();
            MediaBrowserContext.RealeaseWriteprotection();

            //aufräumen
            if (MediaBrowserContext.DBTempFolder != null)
            {
                foreach (string file in System.IO.Directory.GetFiles(MediaBrowserContext.DBTempFolder))
                {
                    try
                    {
                        System.IO.File.Delete(file);
                    }
                    catch { }
                }
                try
                {
                    System.IO.Directory.Delete(MediaBrowserContext.DBTempFolder);
                }
                catch { }
            }
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var comException = e.Exception as System.Runtime.InteropServices.COMException;

            if (comException != null && comException.ErrorCode == -2147221040)
            {
                e.Handled = true;
            }
            else
            {

                MediaBrowserContext.RealeaseWriteprotection();
                Log.Exception(e.Exception);
                e.Handled = true;

                MessageBox.Show("Ein Fehler konnte nicht abgefangen werden!\r\n\r\n" + e.Exception, "MediabrowserWpf wird geschlossen");

                if (Application.Current != null)
                {
                    Application.Current.Shutdown();
                }
            }
        }        
    }
}
