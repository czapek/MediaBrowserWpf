using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MediaBrowser4.DB.Api;

namespace MediaBrowserWPF.Utilities
{
    public sealed class ApiBusyOverlay : IApiRequestObserver
    {
        public T Execute<T>(ApiRequestInfo request, Func<CancellationToken, T> operation)
        {
            Dispatcher dispatcher = Application.Current == null ? null : Application.Current.Dispatcher;

            if (dispatcher == null || !dispatcher.CheckAccess())
                return operation(CancellationToken.None);

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                Window owner = Application.Current == null ? null : Application.Current.MainWindow;
                bool ownerWasEnabled = owner == null || owner.IsEnabled;

                if (owner != null && owner.IsVisible)
                    owner.IsEnabled = false;

                Task<T> task = Task.Run(() => operation(cancellation.Token));
                DispatcherFrame frame = new DispatcherFrame();
                Stopwatch stopwatch = Stopwatch.StartNew();
                Window overlay = null;
                DispatcherTimer delayTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                DispatcherTimer elapsedTimer = null;

                delayTimer.Tick += (sender, args) =>
                {
                    delayTimer.Stop();

                    try
                    {
                        overlay = CreateOverlay(request, cancellation, stopwatch);
                        overlay.Show();
                        elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                        elapsedTimer.Tick += (timerSender, timerArgs) =>
                        {
                            TextBlock elapsedText = overlay.Tag as TextBlock;

                            if (elapsedText != null)
                                elapsedText.Text = "Dauer: " + stopwatch.Elapsed.TotalSeconds.ToString("0.0") + " s";
                        };
                        elapsedTimer.Start();
                    }
                    catch
                    {
                        overlay = null;
                    }
                };

                task.ContinueWith(completed =>
                {
                    dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        delayTimer.Stop();

                        if (elapsedTimer != null)
                            elapsedTimer.Stop();

                        if (overlay != null)
                        {
                            overlay.Close();
                            overlay = null;
                        }

                        if (owner != null && ownerWasEnabled)
                            owner.IsEnabled = true;

                        frame.Continue = false;
                    }));
                }, TaskScheduler.Default);

                delayTimer.Start();
                Dispatcher.PushFrame(frame);
                stopwatch.Stop();

                return task.GetAwaiter().GetResult();
            }
        }

        private static Window CreateOverlay(ApiRequestInfo request, CancellationTokenSource cancellation,
            Stopwatch stopwatch)
        {
            TextBlock title = new TextBlock
            {
                Text = "API-Anfrage läuft",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };

            TextBlock description = new TextBlock
            {
                Text = request.Description,
                Margin = new Thickness(0, 5, 0, 8),
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap
            };

            ProgressBar progressBar = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 8,
                Margin = new Thickness(0, 0, 0, 8)
            };

            TextBlock elapsedText = new TextBlock
            {
                Text = "Dauer: 0,0 s",
                Foreground = Brushes.LightGray
            };

            TextBlock statusText = new TextBlock
            {
                Text = "Wird geladen...",
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };

            Button cancelButton = new Button
            {
                Content = "Abbrechen",
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 95,
                Padding = new Thickness(10, 3, 10, 3)
            };

            cancelButton.Click += (sender, args) =>
            {
                cancellation.Cancel();
                cancelButton.IsEnabled = false;
                statusText.Text = "Abbruch angefordert...";
            };

            StackPanel panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(description);
            panel.Children.Add(progressBar);
            panel.Children.Add(elapsedText);
            panel.Children.Add(statusText);
            panel.Children.Add(cancelButton);

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 25, 25, 25)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(18),
                Child = panel
            };

            Window window = new Window
            {
                Width = 460,
                Height = 185,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = border,
                Tag = elapsedText
            };

            window.Closed += (sender, args) =>
            {
            };

            return window;
        }
    }
}
