using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using MediaBrowser4;
using System.IO;
using MediaBrowser4.DB.Maria;
using MediaBrowser4.DB.Api;
using MediaBrowser4.Utilities;
using System.Windows.Markup;
using System.Globalization;
using System.Runtime.InteropServices;
using MySqlConnector;

namespace MediaBrowserWPF
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        private static readonly string UsageText =
            "Aufruf:" + Environment.NewLine +
            "  MediaBrowserWPF.exe [<db.mb4>] [<init-folder>]" + Environment.NewLine +
            "  MediaBrowserWPF.exe [<init-folder>]" + Environment.NewLine +
            "  MediaBrowserWPF.exe --provider sqlite [--db-file <db.mb4>] [--update]" + Environment.NewLine +
            "  MediaBrowserWPF.exe --provider mariadb --db-host <host> [--db-port <port>] " +
            "--db-name <database> --db-user <user> [--db-password <password> | " +
            "--db-password-env <ENV_NAME> | --db-password-file <path>] " +
            "[--db-ssl-mode None|Preferred|Required|VerifyCA|VerifyFull] [--update] [<init-folder>]" + Environment.NewLine +
            "  MediaBrowserWPF.exe --provider api --api-base-url <url> " +
            "[--api-tenant <tenant>] [--api-key <key> | --api-user <user> [--api-password <password> | " +
            "--api-password-env <ENV_NAME> | --api-password-file <path>]] " +
            "[--api-timeout <seconds>] [<init-folder>]";

        private static string[] startupArguments = new string[0];

        internal static bool SuppressPropertySaveOnExit { get; set; }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        private static void EnableConsoleOutput()
        {
            try
            {
                AttachConsole(0xFFFFFFFF);
            }
            catch
            {
            }
        }

        private static void WriteUpdateProgress(string message)
        {
            try
            {
                Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message);
                Console.Out.Flush();
            }
            catch
            {
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            startupArguments = e.Args ?? new string[0];

            SplashScreen ss = new SplashScreen("Images\\Splash.jpg");
            ss.Show(true, true);

            StartupArguments arguments;

            try
            {
                arguments = ParseArguments(e.Args);
            }
            catch (Exception ex)
            {
                ShowStartupError(ex.Message);
                return;
            }

            if (arguments.Help)
            {
                MessageBox.Show(UsageText, "MediaBrowserWPF");
                Shutdown();
                return;
            }

            if (arguments.Provider == "api")
            {
                ApiRequestObserverHost.Observer = new MediaBrowserWPF.Utilities.ApiBusyOverlay();
            }

            Action<string> progress = null;

            if (arguments.Provider == "mariadb" && arguments.Update)
            {
                EnableConsoleOutput();
                progress = WriteUpdateProgress;
                progress("MediaBrowser MariaDB-Update gestartet.");
            }
            else if (arguments.Provider == "api")
            {
                EnableConsoleOutput();
                progress = WriteUpdateProgress;
                progress("MediaBrowser API-Start gestartet.");
            }

            bool initialized;

            if (arguments.Provider == "mariadb")
            {
                initialized = MediaBrowserContext.Init(arguments.MariaDb, arguments.Update, progress);
            }
            else if (arguments.Provider == "api")
            {
                initialized = MediaBrowserContext.Init(arguments.Api, progress);
            }
            else
            {
                initialized = MediaBrowserContext.Init(arguments.SqlitePath);
            }

            if (!initialized)
            {
                string errorMessage =
                    "Die Datenbank konnte nicht ge?ffnet werden." + Environment.NewLine + Environment.NewLine +
                    (String.IsNullOrWhiteSpace(MediaBrowserContext.LastInitError)
                        ? (arguments.Provider == "mariadb"
                            ? "Pr?fen Sie die Verbindungsdaten. Bei einer neuen oder unvollst?ndigen Datenbank starten Sie mit --update."
                            : (arguments.Provider == "api"
                                ? "Pr?fen Sie die API-URL und die Zugangsdaten."
                                : "Pr?fen Sie den Pfad zur SQLite-Datenbank."))
                        : MediaBrowserContext.LastInitError);

                if (progress != null)
                {
                    progress("FEHLER: " + errorMessage);
                }

                ShowStartupError(errorMessage);
                return;
            }

            if (progress != null)
            {
                progress("Update erfolgreich abgeschlossen.");
                progress("MediaBrowser wird gestartet...");
            }

            MediaBrowser4.MediaBrowserContext.SetDirectShowExtensions
                = MediaBrowserWPF.Properties.Settings.Default.DirectShowExtensions;

            MediaBrowser4.MediaBrowserContext.SetRGBExtensions
                = MediaBrowserWPF.Properties.Settings.Default.RGBExtensions;

            MediaBrowser4.MediaBrowserContext.SetAudioExtraFiles
                = MediaBrowserWPF.Properties.Settings.Default.AudioExtraFiles;

            MainWindow mainWindow = new MainWindow();
            mainWindow.InitPath = arguments.InitPath;
            mainWindow.InitFolder = arguments.InitFolder;
            mainWindow.Show();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception exception = e.ExceptionObject as Exception;

                if (exception != null)
                {
                    Log.Exception(exception, "AppDomain.UnhandledException");
                }
            }
            catch
            {
            }
        }

        private static void ShowStartupError(string message)
        {
            MessageBox.Show(message, "MediaBrowserWPF");
            Application.Current.Shutdown();
        }

        internal static void RestartApplication()
        {
            SuppressPropertySaveOnExit = true;

            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule.FileName;
                string arguments = String.Join(" ", startupArguments.Select(QuoteArgument));

                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "MediaBrowser konnte nicht automatisch neu gestartet werden." + Environment.NewLine +
                    Environment.NewLine + ex.Message,
                    "Neustart fehlgeschlagen");

                return;
            }

            Application.Current.Shutdown();
        }

        private static string QuoteArgument(string argument)
        {
            if (String.IsNullOrEmpty(argument))
                return "\"\"";

            if (argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
                return argument;

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashes = 0;

            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0)
                        builder.Append('\\', backslashes);

                    builder.Append(c);
                    backslashes = 0;
                }
            }

            if (backslashes > 0)
                builder.Append('\\', backslashes * 2);

            builder.Append('"');
            return builder.ToString();
        }

        private static StartupArguments ParseArguments(string[] args)
        {
            StartupArguments result = new StartupArguments();
            List<string> positional = new List<string>();
            int passwordSourceCount = 0;
            int apiPasswordSourceCount = 0;

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i] ?? String.Empty;

                switch (argument.ToLowerInvariant())
                {
                    case "--provider":
                        result.Provider = RequiredValue(args, ref i, argument).ToLowerInvariant();
                        result.ProviderSpecified = true;
                        break;
                    case "--db-file":
                        result.DbFile = RequiredValue(args, ref i, argument);
                        break;
                    case "--db-host":
                        result.DbHost = RequiredValue(args, ref i, argument);
                        break;
                    case "--db-port":
                        uint port;
                        string portValue = RequiredValue(args, ref i, argument);

                        if (!uint.TryParse(portValue, out port) || port == 0 || port > 65535)
                        {
                            throw new ArgumentException("Ungültiger Port: " + portValue);
                        }

                        result.DbPort = port;
                        break;
                    case "--db-name":
                        result.DbName = RequiredValue(args, ref i, argument);
                        break;
                    case "--db-user":
                        result.DbUser = RequiredValue(args, ref i, argument);
                        break;
                    case "--db-password":
                        result.DbPassword = RequiredValue(args, ref i, argument);
                        passwordSourceCount++;
                        break;
                    case "--db-password-env":
                        result.DbPasswordEnv = RequiredValue(args, ref i, argument);
                        passwordSourceCount++;
                        break;
                    case "--db-password-file":
                        result.DbPasswordFile = RequiredValue(args, ref i, argument);
                        passwordSourceCount++;
                        break;
                    case "--db-ssl-mode":
                        MySqlSslMode sslMode;
                        string sslModeValue = RequiredValue(args, ref i, argument);

                        if (!Enum.TryParse<MySqlSslMode>(sslModeValue, true, out sslMode))
                        {
                            throw new ArgumentException("Ung�ltiger SSL-Modus: " + sslModeValue);
                        }

                        result.DbSslMode = sslMode;
                        break;
                    case "--api-base-url":
                        result.ApiBaseUrl = RequiredValue(args, ref i, argument);
                        break;
                    case "--api-tenant":
                        result.ApiTenant = RequiredValue(args, ref i, argument);
                        break;
                    case "--api-key":
                        result.ApiKey = RequiredValue(args, ref i, argument);
                        break;
                    case "--api-user":
                        result.ApiUser = RequiredValue(args, ref i, argument);
                        break;
                    case "--api-password":
                        result.ApiPassword = RequiredValue(args, ref i, argument);
                        apiPasswordSourceCount++;
                        break;
                    case "--api-password-env":
                        result.ApiPasswordEnv = RequiredValue(args, ref i, argument);
                        apiPasswordSourceCount++;
                        break;
                    case "--api-password-file":
                        result.ApiPasswordFile = RequiredValue(args, ref i, argument);
                        apiPasswordSourceCount++;
                        break;
                    case "--api-timeout":
                        int apiTimeout;

                        if (!Int32.TryParse(RequiredValue(args, ref i, argument), out apiTimeout)
                            || apiTimeout <= 0)
                        {
                            throw new ArgumentException("Ung�ltiger API-Timeout.");
                        }

                        result.ApiTimeoutSeconds = apiTimeout;
                        break;
                    case "--update":
                        result.Update = true;
                        break;
                    case "--help":
                    case "-h":
                    case "-?":
                        result.Help = true;
                        break;
                    default:
                        if (argument.StartsWith("-", StringComparison.Ordinal))
                        {
                            throw new ArgumentException("Unbekanntes Argument: " + argument);
                        }

                        positional.Add(argument);
                        break;
                }
            }

            if (passwordSourceCount > 1)
            {
                throw new ArgumentException("Es darf nur eine Passwortquelle angegeben werden.");
            }

            if (apiPasswordSourceCount > 1)
            {
                throw new ArgumentException("Es darf nur eine API-Passwortquelle angegeben werden.");
            }

            if (result.Provider != "sqlite" && result.Provider != "mariadb" && result.Provider != "api")
            {
                throw new ArgumentException("Unbekannter Provider: " + result.Provider);
            }

            if (result.Help)
            {
                return result;
            }

            if (result.Provider == "mariadb")
            {
                ParseMariaDbArguments(result, positional, passwordSourceCount);
            }
            else if (result.Provider == "api")
            {
                ParseApiArguments(result, positional, apiPasswordSourceCount);
            }
            else
            {
                ParseSqliteArguments(result, positional);
            }

            return result;
        }

        private static void ParseMariaDbArguments(StartupArguments result, List<string> positional,
            int passwordSourceCount)
        {
            if (String.IsNullOrWhiteSpace(result.DbHost))
            {
                throw new ArgumentException("--db-host ist erforderlich.");
            }

            if (String.IsNullOrWhiteSpace(result.DbName))
            {
                throw new ArgumentException("--db-name ist erforderlich.");
            }

            if (String.IsNullOrWhiteSpace(result.DbUser))
            {
                throw new ArgumentException("--db-user ist erforderlich.");
            }

            if (result.DbFile != null)
            {
                throw new ArgumentException("--db-file ist nur für --provider sqlite gültig.");
            }

            string password = null;

            if (passwordSourceCount == 0)
            {
                password = Environment.GetEnvironmentVariable("MEDIABROWSER_DB_PASSWORD");
            }
            else if (result.DbPasswordEnv != null)
            {
                password = Environment.GetEnvironmentVariable(result.DbPasswordEnv);

                if (password == null)
                {
                    throw new ArgumentException("Die Umgebungsvariable " + result.DbPasswordEnv + " ist nicht gesetzt.");
                }
            }
            else if (result.DbPasswordFile != null)
            {
                if (!File.Exists(result.DbPasswordFile))
                {
                    throw new ArgumentException("Passwortdatei nicht gefunden: " + result.DbPasswordFile);
                }

                password = File.ReadAllText(result.DbPasswordFile).TrimEnd('\r', '\n');
            }
            else
            {
                password = result.DbPassword;
            }

            if (positional.Count > 1)
            {
                throw new ArgumentException("Zu viele Argumente für MariaDB.");
            }

            if (positional.Count == 1)
            {
                if (Directory.Exists(positional[0]))
                {
                    result.InitFolder = positional[0];
                }
                else if (IsSqliteDatabasePath(positional[0]))
                {
                    Log.Info("SQLite-Pfad im MariaDB-Modus ignoriert: " + positional[0]);
                }
                else
                {
                    throw new ArgumentException("Init-Ordner nicht gefunden: " + positional[0]);
                }
            }

            result.MariaDb = new MariaDbConnectionOptions
            {
                Host = result.DbHost,
                Port = result.DbPort,
                Database = result.DbName,
                User = result.DbUser,
                Password = password,
                SslMode = result.DbSslMode
            };
        }

        private static void ParseApiArguments(StartupArguments result, List<string> positional,
            int apiPasswordSourceCount)
        {
            if (String.IsNullOrWhiteSpace(result.ApiBaseUrl))
            {
                throw new ArgumentException("--api-base-url ist erforderlich.");
            }

            if (result.Update)
            {
                throw new ArgumentException("--update wird vom API-Provider nicht unterstützt.");
            }

            string password = null;

            if (apiPasswordSourceCount == 0)
            {
                password = Environment.GetEnvironmentVariable("MEDIABROWSER_API_PASSWORD");
            }
            else if (result.ApiPasswordEnv != null)
            {
                password = Environment.GetEnvironmentVariable(result.ApiPasswordEnv);

                if (password == null)
                {
                    throw new ArgumentException("Die Umgebungsvariable " + result.ApiPasswordEnv + " ist nicht gesetzt.");
                }
            }
            else if (result.ApiPasswordFile != null)
            {
                if (!File.Exists(result.ApiPasswordFile))
                {
                    throw new ArgumentException("API-Passwortdatei nicht gefunden: " + result.ApiPasswordFile);
                }

                password = File.ReadAllText(result.ApiPasswordFile).TrimEnd('\r', '\n');
            }
            else
            {
                password = result.ApiPassword;
            }

            if (positional.Count > 1)
            {
                throw new ArgumentException("Zu viele Argumente für die API.");
            }

            if (positional.Count == 1)
            {
                if (Directory.Exists(positional[0]))
                {
                    result.InitFolder = positional[0];
                }
                else if (IsSqliteDatabasePath(positional[0]))
                {
                    Log.Info("SQLite-Pfad im API-Modus ignoriert: " + positional[0]);
                }
                else
                {
                    throw new ArgumentException("Init-Ordner nicht gefunden: " + positional[0]);
                }
            }

            result.Api = new ApiConnectionOptions
            {
                BaseUrl = result.ApiBaseUrl,
                ApiKey = result.ApiKey,
                Username = result.ApiUser,
                Password = password,
                Tenant = result.ApiTenant,
                TimeoutSeconds = result.ApiTimeoutSeconds
            };

            result.Api.BuildApiBaseUrl();
        }

        private static void ParseSqliteArguments(StartupArguments result, List<string> positional)
        {
            if (positional.Count > 2)
            {
                throw new ArgumentException("Zu viele Argumente für SQLite.");
            }

            if (result.DbFile != null)
            {
                result.SqlitePath = result.DbFile;

                if (positional.Count == 1)
                {
                    if (!Directory.Exists(positional[0]))
                    {
                        throw new ArgumentException("Init-Ordner nicht gefunden: " + positional[0]);
                    }

                    result.InitFolder = positional[0];
                }

                return;
            }

            if (positional.Count == 0)
            {
                foreach (string part in MediaBrowserWPF.Properties.Settings.Default.DBPath.Split(';'))
                {
                    if (File.Exists(part))
                    {
                        result.SqlitePath = part;
                        break;
                    }
                }

                return;
            }

            if (File.Exists(positional[0]))
            {
                result.SqlitePath = positional[0];

                if (positional.Count == 2)
                {
                    if (!Directory.Exists(positional[1]))
                    {
                        throw new ArgumentException("Init-Ordner nicht gefunden: " + positional[1]);
                    }

                    result.InitFolder = positional[1];
                }
            }
            else if (Directory.Exists(positional[0]))
            {
                if (positional.Count == 2)
                {
                    throw new ArgumentException("Zu viele Argumente für SQLite.");
                }

                result.InitPath = positional[0];
            }
            else if (result.ProviderSpecified)
            {
                result.SqlitePath = positional[0];
            }
            else
            {
                throw new ArgumentException("Pfad nicht gefunden: " + positional[0]);
            }
        }

        private static string RequiredValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("Für " + option + " fehlt ein Wert.");
            }

            index++;
            return args[index];
        }

        private static bool IsSqliteDatabasePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                return false;

            string extension = Path.GetExtension(path);

            return String.Equals(extension, ".mb4", StringComparison.OrdinalIgnoreCase)
                || String.Equals(extension, ".mb4t", StringComparison.OrdinalIgnoreCase);
        }

        private class StartupArguments
        {
            public string Provider = "sqlite";
            public bool ProviderSpecified;
            public string SqlitePath;
            public string InitPath;
            public string InitFolder;
            public string DbFile;
            public string DbHost;
            public uint DbPort = 3306;
            public string DbName;
            public string DbUser;
            public string DbPassword;
            public string DbPasswordEnv;
            public string DbPasswordFile;
            public MySqlSslMode DbSslMode = MySqlSslMode.Preferred;
            public string ApiBaseUrl;
            public string ApiTenant;
            public string ApiKey;
            public string ApiUser;
            public string ApiPassword;
            public string ApiPasswordEnv;
            public string ApiPasswordFile;
            public int ApiTimeoutSeconds = 60;
            public bool Update;
            public bool Help;
            public MariaDbConnectionOptions MariaDb;
            public ApiConnectionOptions Api;
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

            if (!SuppressPropertySaveOnExit)
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
