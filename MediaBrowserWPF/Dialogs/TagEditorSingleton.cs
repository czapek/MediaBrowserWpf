using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using MediaBrowser4.Objects;
using System.Windows.Forms;

namespace MediaBrowserWPF.Dialogs
{
    public static class TagEditorSingleton
    {
        public static void ShowTagEditor(MediaItem mediaItem, Point point, Window window)
        {
            ShowTagEditor(new List<MediaItem>() { mediaItem }, point, window);
        }

        private static Dictionary<Window, TagEditor> editorDic = new Dictionary<Window, TagEditor>();
        public static void ShowTagEditor(List<MediaItem> mediaItemList, Point point, Window window)
        {
            if (!editorDic.ContainsKey(window))
            {
                editorDic[window] = new TagEditor();
                editorDic[window].Owner = window;
                window.Closed += new EventHandler(window_Closed);
                window.PreviewMouseDown += Window_PreviewMouseDown;
                window.PreviewKeyDown += Window_PreviewKeyDown;
            }

            editorDic[window].MediaItemList = mediaItemList;
            editorDic[window].Left = point.X;
            editorDic[window].Top = point.Y;

            if (window is MainWindow)
            {
                editorDic[window].Show();
            }
            else
            {
                editorDic[window].ShowDialog();
            }
        }

        public static bool IsVisble(Window window)
        {
            if (editorDic.ContainsKey(window))
            {
                return editorDic[window].IsVisible;
            }
            else
            {
                return false;
            }
        }

        private static void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && editorDic.ContainsKey((Window)sender))
                editorDic[(Window)sender].Close();
        }

        private static void Window_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (editorDic.ContainsKey((Window)sender))
                editorDic[(Window)sender].Close();
        }

        static void window_Closed(object sender, EventArgs e)
        {
            if (editorDic.ContainsKey((Window)sender))
                editorDic.Remove((Window)sender);
        }
    }
}
