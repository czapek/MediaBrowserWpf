using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MediaBrowser4.DB.Maria;
using MediaBrowser4.DB.Migration;
using MediaBrowser4.Utilities;

namespace MediaBrowserWPF.Dialogs
{
    public partial class SqliteToMariaDbMigrationDialog : Window
    {
        private readonly MariaDbConnectionOptions targetOptions;
        private readonly string targetDatabase;
        private MigrationPreflight preflight;
        private CancellationTokenSource cancellation;
        private bool busy;
        private bool migrationStarted;
        private bool allowClose;

        public SqliteToMariaDbMigrationDialog(MariaDbConnectionOptions targetOptions)
        {
            if (targetOptions == null)
                throw new ArgumentNullException("targetOptions");

            this.targetOptions = targetOptions;
            this.targetDatabase = targetOptions.Database;

            InitializeComponent();

            this.TargetHostTextBox.Text = targetOptions.Host;
            this.TargetPortTextBox.Text = targetOptions.Port.ToString(CultureInfo.InvariantCulture);
            this.TargetDatabaseTextBox.Text = targetOptions.Database;
            this.TargetUserTextBox.Text = targetOptions.User;
            this.TargetPasswordBox.Password = targetOptions.Password ?? String.Empty;

            this.SourcePathTextBox.Text = GetInitialSourcePath();

            if (!String.IsNullOrWhiteSpace(this.SourcePathTextBox.Text))
                TrySetThumbnailPath();

            this.SummaryTextBox.Text =
                "Bitte Quell- und Zieldatenbank prüfen. Die Prüfung ermittelt Zeilenzahlen und Warnungen, " +
                "ohne die Zieldatenbank zu verändern.";

            UpdateStartState();
        }

        private static string GetInitialSourcePath()
        {
            string dbPath = MediaBrowserWPF.Properties.Settings.Default.DBPath;

            if (String.IsNullOrWhiteSpace(dbPath))
                return String.Empty;

            foreach (string part in dbPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (File.Exists(part))
                    return part;
            }

            return String.Empty;
        }

        private void TrySetThumbnailPath()
        {
            if (!String.IsNullOrWhiteSpace(this.ThumbnailPathTextBox.Text)
                && File.Exists(this.ThumbnailPathTextBox.Text))
            {
                return;
            }

            try
            {
                this.ThumbnailPathTextBox.Text =
                    SqliteToMariaDbMigration.FindThumbnailDatabase(this.SourcePathTextBox.Text);
            }
            catch
            {
            }
        }

        private void BrowseSourceButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MediaBrowser-Datenbank (*.mb4)|*.mb4|Alle Dateien (*.*)|*.*",
                CheckFileExists = true
            };

            string currentPath = this.SourcePathTextBox.Text;

            if (File.Exists(currentPath))
                dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
            else if (Directory.Exists(currentPath))
                dialog.InitialDirectory = currentPath;

            if (dialog.ShowDialog(this) == true)
            {
                this.SourcePathTextBox.Text = dialog.FileName;
                this.ThumbnailPathTextBox.Text = String.Empty;
                TrySetThumbnailPath();
            }
        }

        private void BrowseThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MediaBrowser-Thumbnaildatenbank (*.mb4t)|*.mb4t|Alle Dateien (*.*)|*.*",
                CheckFileExists = true
            };

            string currentPath = this.ThumbnailPathTextBox.Text;

            if (File.Exists(currentPath))
                dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
            else if (Directory.Exists(Path.GetDirectoryName(this.SourcePathTextBox.Text)))
                dialog.InitialDirectory = Path.GetDirectoryName(this.SourcePathTextBox.Text);

            if (dialog.ShowDialog(this) == true)
                this.ThumbnailPathTextBox.Text = dialog.FileName;
        }

        private void SourcePathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            InvalidatePreflight();
        }

        private void ThumbnailPathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            InvalidatePreflight();
        }

        private void TargetPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            InvalidatePreflight();
        }

        private void Confirmation_Changed(object sender, RoutedEventArgs e)
        {
            UpdateStartState();
        }

        private void ConfirmationTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateStartState();
        }

        private void InvalidatePreflight()
        {
            if (this.busy)
                return;

            this.preflight = null;

            if (this.SummaryTextBox != null)
                this.SummaryTextBox.Text = String.Empty;

            if (this.MigrationProgressBar != null)
            {
                this.MigrationProgressBar.IsIndeterminate = false;
                this.MigrationProgressBar.Value = 0;
            }

            if (this.StatusTextBlock != null)
                this.StatusTextBlock.Text = "Bereit.";

            if (this.TableStatusTextBlock != null)
                this.TableStatusTextBlock.Text = String.Empty;

            UpdateStartState();
        }

        private void UpdateStartState()
        {
            if (this.StartButton == null)
                return;

            bool confirmed = this.ConfirmationCheckBox.IsChecked == true
                && String.Equals(this.ConfirmationTextBox.Text.Trim(), this.targetDatabase, StringComparison.Ordinal);

            this.StartButton.IsEnabled = !this.busy && this.preflight != null && confirmed;
            this.PreflightButton.IsEnabled = !this.busy;
            this.CloseButton.IsEnabled = !this.busy || this.migrationStarted;

            if (this.busy)
                this.CloseButton.Content = this.migrationStarted ? "Abbruch anfordern" : "Bitte warten...";
            else
                this.CloseButton.Content = "Abbrechen";
        }

        private void SetBusy(bool value)
        {
            this.busy = value;

            this.SourcePathTextBox.IsEnabled = !value;
            this.ThumbnailPathTextBox.IsEnabled = !value;
            this.BrowseSourceButton.IsEnabled = !value;
            this.BrowseThumbnailButton.IsEnabled = !value;
            this.TargetPasswordBox.IsEnabled = !value;
            this.ConfirmationCheckBox.IsEnabled = !value;
            this.ConfirmationTextBox.IsEnabled = !value;

            UpdateStartState();
        }

        private MariaDbConnectionOptions CreateTargetOptions()
        {
            if (String.IsNullOrEmpty(this.TargetPasswordBox.Password))
                throw new InvalidOperationException("Bitte das Passwort der MariaDB-Zieldatenbank eingeben.");

            return new MariaDbConnectionOptions
            {
                Host = this.targetOptions.Host,
                Port = this.targetOptions.Port,
                Database = this.targetOptions.Database,
                User = this.targetOptions.User,
                Password = this.TargetPasswordBox.Password,
                SslMode = this.targetOptions.SslMode,
                ConnectionTimeout = this.targetOptions.ConnectionTimeout,
                AllowLoadLocalInfile = this.targetOptions.AllowLoadLocalInfile
            };
        }

        private bool ValidateInputs(out string error)
        {
            error = null;

            if (!File.Exists(this.SourcePathTextBox.Text))
            {
                error = "Die SQLite-Datenbank wurde nicht gefunden.";
                return false;
            }

            if (String.IsNullOrWhiteSpace(this.ThumbnailPathTextBox.Text))
                TrySetThumbnailPath();

            if (!File.Exists(this.ThumbnailPathTextBox.Text))
            {
                error = "Die Thumbnail-Datenbank wurde nicht gefunden.";
                return false;
            }

            if (String.IsNullOrEmpty(this.TargetPasswordBox.Password))
            {
                error = "Bitte das Passwort der MariaDB-Zieldatenbank eingeben.";
                return false;
            }

            return true;
        }

        private async void PreflightButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.busy)
                return;

            string error;

            if (!ValidateInputs(out error))
            {
                MessageBox.Show(this, error, "SQLite-Migration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.preflight = null;
            this.SummaryTextBox.Text = String.Empty;
            this.StatusTextBlock.Text = "Prüfe Quell- und Zieldatenbank...";
            this.TableStatusTextBlock.Text = String.Empty;
            this.MigrationProgressBar.IsIndeterminate = true;
            SetBusy(true);

            try
            {
                SqliteToMariaDbMigration migration = new SqliteToMariaDbMigration(
                    CreateTargetOptions(), this.SourcePathTextBox.Text, this.ThumbnailPathTextBox.Text);

                MigrationPreflight result = await Task.Run(() => migration.Preflight());

                this.preflight = result;
                this.SummaryTextBox.Text = FormatPreflight(result);
                this.StatusTextBlock.Text = "Prüfung erfolgreich. Die Zieldatenbank wurde nicht verändert.";
            }
            catch (Exception ex)
            {
                this.SummaryTextBox.Text = ex.Message;
                this.StatusTextBlock.Text = "Prüfung fehlgeschlagen.";
                MessageBox.Show(this, ex.Message, "Prüfung fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.MigrationProgressBar.IsIndeterminate = false;
                SetBusy(false);
                UpdateStartState();
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.busy || this.preflight == null)
                return;

            bool confirmed = this.ConfirmationCheckBox.IsChecked == true
                && String.Equals(this.ConfirmationTextBox.Text.Trim(), this.targetDatabase, StringComparison.Ordinal);

            if (!confirmed)
            {
                MessageBox.Show(this,
                    "Bitte die Löschwarnung bestätigen und den Namen der Zieldatenbank eingeben.",
                    "SQLite-Migration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MigrationPreflight currentPreflight = this.preflight;
            SqliteToMariaDbMigration migration;

            try
            {
                migration = new SqliteToMariaDbMigration(CreateTargetOptions(),
                    currentPreflight.SourceMainPath, currentPreflight.SourceThumbnailPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "SQLite-Migration", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.cancellation = new CancellationTokenSource();
            this.migrationStarted = true;
            SetBusy(true);
            this.MigrationProgressBar.IsIndeterminate = false;
            this.MigrationProgressBar.Value = 0;
            this.StatusTextBlock.Text = "Migration wird gestartet...";
            this.TableStatusTextBlock.Text = String.Empty;

            Progress<MigrationProgress> progress = new Progress<MigrationProgress>(UpdateProgress);

            try
            {
                MigrationResult result = await Task.Run(() =>
                    migration.MigrateAsync(currentPreflight, progress, this.cancellation.Token));

                this.preflight = null;
                this.allowClose = true;
                this.migrationStarted = false;
                SetBusy(false);
                this.SummaryTextBox.Text = FormatResult(result);
                this.StatusTextBlock.Text = "Migration erfolgreich abgeschlossen.";
                this.TableStatusTextBlock.Text = String.Empty;
                this.MigrationProgressBar.Value = 100;
                MediaBrowserWPF.App.SuppressPropertySaveOnExit = true;

                MessageBox.Show(this,
                    "Die Migration wurde erfolgreich abgeschlossen." + Environment.NewLine + Environment.NewLine +
                    "Dauer: " + result.Elapsed + Environment.NewLine +
                    "Migrierte Zeilen: " + result.TotalRows.ToString("N0") + Environment.NewLine + Environment.NewLine +
                    "MediaBrowser wird jetzt neu gestartet.",
                    "Migration erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);

                MediaBrowserWPF.App.RestartApplication();
            }
            catch (OperationCanceledException)
            {
                this.preflight = null;
                this.allowClose = true;
                this.migrationStarted = false;
                SetBusy(false);
                this.StatusTextBlock.Text = "Migration abgebrochen.";
                MediaBrowserWPF.App.SuppressPropertySaveOnExit = true;

                MessageBox.Show(this,
                    "Die Migration wurde abgebrochen. Die Zieldatenbank ist unvollständig und muss erneut migriert werden." +
                    Environment.NewLine + Environment.NewLine + "MediaBrowser wird beendet.",
                    "Migration abgebrochen", MessageBoxButton.OK, MessageBoxImage.Warning);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                this.preflight = null;
                this.allowClose = true;
                this.migrationStarted = false;
                SetBusy(false);
                this.SummaryTextBox.Text = ex.ToString();
                this.StatusTextBlock.Text = "Migration fehlgeschlagen.";
                MediaBrowserWPF.App.SuppressPropertySaveOnExit = true;
                Log.Exception(ex, "SQLite-Migration");

                MessageBox.Show(this,
                    "Die Migration ist fehlgeschlagen. Die Zieldatenbank ist unvollständig und muss erneut migriert werden." +
                    Environment.NewLine + Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine +
                    "MediaBrowser wird beendet.",
                    "Migration fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);

                Application.Current.Shutdown();
            }
            finally
            {
                if (this.cancellation != null)
                {
                    this.cancellation.Dispose();
                    this.cancellation = null;
                }
            }
        }

        private void UpdateProgress(MigrationProgress progress)
        {
            if (progress == null || this.preflight == null)
                return;

            if (!String.IsNullOrEmpty(progress.Message))
                this.StatusTextBlock.Text = progress.Message;

            if (progress.TotalRows > 0)
            {
                double value = progress.RowsProcessed * 100.0 / progress.TotalRows;
                this.MigrationProgressBar.Value = Math.Max(0, Math.Min(100, value));
            }

            if (!String.IsNullOrEmpty(progress.TableName) && progress.TableCount > 0)
            {
                this.TableStatusTextBlock.Text = "Tabelle " + progress.TableIndex + "/" + progress.TableCount
                    + ": " + progress.TableName + " (" + progress.RowsProcessed.ToString("N0") + " Zeilen)";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.busy)
            {
                if (this.migrationStarted && this.cancellation != null)
                {
                    this.cancellation.Cancel();
                    this.CloseButton.IsEnabled = false;
                    this.StatusTextBlock.Text = "Abbruch angefordert...";
                }

                return;
            }

            this.Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.busy && !this.allowClose)
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        private static string FormatPreflight(MigrationPreflight preflight)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Quelle:       " + preflight.SourceMainPath);
            builder.AppendLine("Thumbnails:   " + preflight.SourceThumbnailPath);
            builder.AppendLine("Ziel:         " + preflight.TargetHost + " / " + preflight.TargetDatabase
                + " / " + preflight.TargetUser);
            builder.AppendLine();
            builder.AppendLine("Quellzeilen gesamt: " + preflight.TotalRows.ToString("N0"));

            if (preflight.NormalizedDates > 0)
                builder.AppendLine("CATEGORY.DATE:      " + preflight.NormalizedDates.ToString("N0")
                    + " Werte vor 1000-01-01 werden auf NULL gesetzt.");

            builder.AppendLine();
            builder.AppendLine("Quelltabellen:");
            AppendCounts(builder, preflight.SourceCounts, false);
            builder.AppendLine();
            builder.AppendLine("Zieltabellen vor der Migration:");
            AppendCounts(builder, preflight.TargetCounts, true);
            builder.AppendLine();
            builder.AppendLine("Hinweis: FACES wird nicht migriert und bleibt leer.");

            if (preflight.Warnings != null && preflight.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Hinweise:");

                foreach (string warning in preflight.Warnings)
                    builder.AppendLine("  - " + warning);
            }

            return builder.ToString();
        }

        private static string FormatResult(MigrationResult result)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Migration abgeschlossen.");
            builder.AppendLine("Dauer:        " + result.Elapsed);
            builder.AppendLine("Quellzeilen:  " + result.TotalRows.ToString("N0"));
            builder.AppendLine();
            builder.AppendLine("Zieltabellen nach der Migration:");
            AppendCounts(builder, result.TargetCounts, true);

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Hinweise:");

                foreach (string warning in result.Warnings)
                    builder.AppendLine("  - " + warning);
            }

            return builder.ToString();
        }

        private static void AppendCounts(StringBuilder builder, IDictionary<string, long> counts, bool onlyNonEmpty)
        {
            if (counts == null || counts.Count == 0)
            {
                builder.AppendLine("  (keine)");
                return;
            }

            long total = 0;

            foreach (KeyValuePair<string, long> entry in counts.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                total += entry.Value;

                if (!onlyNonEmpty || entry.Value > 0)
                    builder.AppendLine("  " + entry.Key.PadRight(28) + entry.Value.ToString("N0"));
            }

            if (onlyNonEmpty && total == 0)
                builder.AppendLine("  (leer)");
        }
    }
}
