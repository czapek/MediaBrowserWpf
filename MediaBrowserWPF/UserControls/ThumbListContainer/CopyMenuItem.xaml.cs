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
using System.Windows.Navigation;
using System.Windows.Shapes;
using MediaBrowser4.Objects;
using System.IO;
using MediaBrowserWPF.Utilities;
using System.ComponentModel;
using MediaBrowser4;
using MediaBrowser4.Utilities;

namespace MediaBrowserWPF.UserControls.ThumbListContainer
{
    /// <summary>
    /// Interaktionslogik für CopyMenuItem.xaml
    /// </summary>
    public partial class CopyMenuItem : MenuItem
    {
        public class CopyPropertiesArgs : EventArgs
        {
            public string Message { get; set; }
            public bool MustRedraw { get; set; }
            public List<Category> CopiedCategories { get; set; }
        }

        public event EventHandler<CopyPropertiesArgs> PasteAction;
        public event EventHandler<CopyPropertiesArgs> CopyAction;

        public List<MediaItem> MediaItemList
        {
            get;
            set;
        }

        public CopyMenuItem()
        {
            InitializeComponent();

            this.MenuItemCategories.IsChecked = MediaBrowserContext.IsCheckedMenuItemCategories;
            this.MenuItemTrim.IsChecked = MediaBrowserContext.IsCheckedMenuItemTrim;
            this.MenuItemOrientate.IsChecked = MediaBrowserContext.IsCheckedMenuItemOrientate;
            this.MenuItemCrop.IsChecked = MediaBrowserContext.IsCheckedMenuItemCrop;
            this.MenuItemClip.IsChecked = MediaBrowserContext.IsCheckedMenuItemClip;
            this.MenuItemRotate.IsChecked = MediaBrowserContext.IsCheckedMenuItemRotate;
            this.MenuItemLevels.IsChecked = MediaBrowserContext.IsCheckedMenuItemLevels;
            this.MenuItemZoom.IsChecked = MediaBrowserContext.IsCheckedMenuItemZoom;
            this.MenuItemFlip.IsChecked = MediaBrowserContext.IsCheckedMenuItemFlip;
        }

        private void MenuItemCategories_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.IsCheckedMenuItemCategories = this.MenuItemCategories.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemTrim = this.MenuItemTrim.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemOrientate = this.MenuItemOrientate.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemCrop = this.MenuItemCrop.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemClip = this.MenuItemClip.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemRotate = this.MenuItemRotate.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemLevels = this.MenuItemLevels.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemZoom = this.MenuItemZoom.IsChecked;
            MediaBrowserContext.IsCheckedMenuItemFlip = this.MenuItemFlip.IsChecked;
        }

        private void MenuItemCopyToDesktop_Click(object sender, RoutedEventArgs e)
        {
            string path = MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder();
            FileInfo[] fileObjects = this.MediaItemList.Where(x => File.Exists(x.FullName)).Select(x => x.FileObject).ToArray();

            if (fileObjects.Length == 1)
            {
                if (!System.IO.File.Exists(System.IO.Path.Combine(path, fileObjects[0].Name)))
                {
                    MainWindow.GiveShortFeedback();
                    fileObjects[0].CopyTo(System.IO.Path.Combine(path, fileObjects[0].Name));
                }
            }
            else
            {
                MainWindow.BussyIndicatorContent = "Kopiere " + this.MediaItemList.Count + " Dateien ...";
                MainWindow.BussyIndicatorIsBusy = true;

                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += (o, ea) =>
                {
                    try
                    {
                        foreach (FileInfo fInfo in fileObjects)
                        {
                            if (!System.IO.File.Exists(System.IO.Path.Combine(path, fInfo.Name)))
                                fInfo.CopyTo(System.IO.Path.Combine(path, fInfo.Name));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                };

                worker.RunWorkerCompleted += (o, ea) =>
                {
                    MainWindow.BussyIndicatorIsBusy = false;
                };

                worker.RunWorkerAsync();
            }
        }

        private void MenuItemCopyFileToClipboard_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.GiveShortFeedback();
            System.Collections.Specialized.StringCollection paths = new System.Collections.Specialized.StringCollection();
            foreach (MediaItem mItem in this.MediaItemList)
                paths.Add(mItem.FullName);

            Clipboard.SetFileDropList(paths);
        }

        private void MenuItemCopyNameToClipboard_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.GiveShortFeedback();
            Clipboard.SetText(
               String.Join(Environment.NewLine, this.MediaItemList.Select(x => System.IO.Path.GetFileNameWithoutExtension(x.FileObject.Name)))
               );
        }

        private void MenuItemCopyPathToClipboard_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.GiveShortFeedback();
            Clipboard.SetText(
               String.Join(Environment.NewLine, this.MediaItemList.Select(x => x.FullName))
               );
        }

        private void MenuItemCopyIdsToClipboard_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.GiveShortFeedback();
            Clipboard.SetText(
               String.Join(",", this.MediaItemList.Select(x => x.Id))
               );
        }

        private void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            this.MenuItemCopyProperties.IsEnabled = this.MediaItemList.Count == 1;
            this.MenuItemPasteProperties.IsEnabled = !MediaBrowserContext.CopyItemProperties.IsEmpty;

            this.MenuItemCategories.Header = "... Kategorien" + (MediaBrowserContext.CopyItemProperties.Categories == null
                ? String.Empty : " (" + MediaBrowserContext.CopyItemProperties.Categories.Count + "x)");
        }

        private void MenuItemCopyProperties_Click(object sender, RoutedEventArgs e)
        {
            this.Copy(this.MediaItemList);
        }

        private void MenuItemPasteProperties_Click(object sender, RoutedEventArgs e)
        {
            this.Paste(this.MediaItemList);
        }

        public void Copy(List<MediaItem> list)
        {
            if (list.Count == 0)
                return;

            this.MediaItemList = list;
            MainWindow.GiveShortFeedback();

            List<string> resultList = new List<string>();

            if (this.MenuItemCategories.IsChecked)
            {
                MediaBrowserContext.CopyItemProperties.Categories = this.MenuItemCategories.IsChecked ?
                    this.MediaItemList.SelectMany(x => x.Categories).Distinct().ToList() : null;

                resultList.Add("Kategorien");
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.Categories = null;
            }

            MediaItem mItem = list[0];

            if (this.MenuItemOrientate.IsChecked)
            {
                MediaBrowserContext.CopyItemProperties.Orientation = mItem.Orientation;
                resultList.Add("Ausrichten");
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.Orientation = null;
            }

            if (this.MenuItemTrim.IsChecked)
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("TRIM");

                if (layer != null)
                {
                    resultList.Add("Videoschnitt");
                    MediaBrowserContext.CopyItemProperties.TrimLayer = layer.Action;
                }
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.TrimLayer = null;
            }

            if (this.MenuItemCrop.IsChecked)
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("CROP");

                if (layer != null)
                {
                    resultList.Add("Zuschneiden");
                    MediaBrowserContext.CopyItemProperties.CropLayer = layer.Action;
                }
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.CropLayer = null;
            }

            if (this.MenuItemClip.IsChecked)
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("CLIP");

                if (layer != null)
                {
                    resultList.Add("Passepartout");
                    MediaBrowserContext.CopyItemProperties.ClipLayer = layer.Action;
                }
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.ClipLayer = null;
            }

            if (this.MenuItemRotate.IsChecked)
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("ROT");

                if (layer != null)
                {
                    resultList.Add("Drehen");
                    MediaBrowserContext.CopyItemProperties.RotateLayer = layer.Action;
                }
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.RotateLayer = null;
            }

            if (this.MenuItemLevels.IsChecked)
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("LEVELS");

                if (layer != null)
                {
                    resultList.Add("Kontrast, Gamma und Farben");
                    MediaBrowserContext.CopyItemProperties.LevelsLayer = layer.Action;
                }
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.LevelsLayer = null;
            }

            if (this.MenuItemZoom.IsChecked)
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("ZOOM");

                if (layer != null)
                {
                    resultList.Add("Größe und Position");
                    MediaBrowserContext.CopyItemProperties.ZoomLayer = layer.Action;
                }
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.ZoomLayer = null;
            }

            if (this.MenuItemFlip.IsChecked)
            {
                MediaBrowser4.Objects.Layer layer = mItem.FindLayer("FLIP");

                if (layer != null)
                {
                    resultList.Add("Spiegeln");
                    MediaBrowserContext.CopyItemProperties.FlipLayer = layer.Action;
                }
            }
            else
            {
                MediaBrowserContext.CopyItemProperties.FlipLayer = null;
            }

            if (this.CopyAction != null)
            {
                this.CopyAction.Invoke(this, new CopyPropertiesArgs() { Message = String.Join(", ", resultList), CopiedCategories = MediaBrowserContext.CopyItemProperties.Categories });
            }
        }

        public void Paste(List<MediaItem> list)
        {
            if (MediaBrowserContext.CopyItemProperties.IsEmpty)
                return;

            this.MediaItemList = list;
            MainWindow.GiveShortFeedback();
            List<string> resultList = new List<string>();

            if (this.MenuItemCategories.IsChecked && MediaBrowserContext.CopyItemProperties.Categories != null)
            {
                MediaBrowserContext.CategorizeMediaItems(this.MediaItemList, MediaBrowserContext.CopyItemProperties.Categories);
                resultList.Add("Kategorien");
            }

            bool mustRedraw = false;
            foreach (MediaItem mItem in list)
            {
                if (this.MenuItemOrientate.IsChecked && MediaBrowserContext.CopyItemProperties.Orientation != null)
                {
                    resultList.Add("Ausrichten");
                    mustRedraw = true;
                    mItem.Orientation = MediaBrowserContext.CopyItemProperties.Orientation.Value;
                    MediaBrowserContext.Rotate90(mItem);
                }

                if (mItem is MediaItemVideo && this.MenuItemTrim.IsChecked && !String.IsNullOrEmpty(MediaBrowserContext.CopyItemProperties.TrimLayer))
                {
                    resultList.Add("Videoschnitt");
                    mustRedraw = true;
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("TRIM");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("TRIM", mItem.Layers.Count);

                    layer.Action = MediaBrowserContext.CopyItemProperties.TrimLayer;
                }

                if (this.MenuItemCrop.IsChecked && !String.IsNullOrEmpty(MediaBrowserContext.CopyItemProperties.CropLayer))
                {
                    resultList.Add("Zuschneiden");
                    mustRedraw = true;
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("CROP");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("CROP", mItem.Layers.Count);

                    layer.Action = MediaBrowserContext.CopyItemProperties.CropLayer;
                }

                if (this.MenuItemClip.IsChecked && !String.IsNullOrEmpty(MediaBrowserContext.CopyItemProperties.ClipLayer))
                {
                    resultList.Add("Passepartout");
                    mustRedraw = true;
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("CLIP");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("CLIP", mItem.Layers.Count);

                    layer.Action = MediaBrowserContext.CopyItemProperties.ClipLayer;
                }

                if (this.MenuItemRotate.IsChecked && !String.IsNullOrEmpty(MediaBrowserContext.CopyItemProperties.RotateLayer))
                {
                    resultList.Add("Drehen");
                    mustRedraw = true;
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("ROT");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("ROT", mItem.Layers.Count);

                    layer.Action = MediaBrowserContext.CopyItemProperties.RotateLayer;
                }

                if (this.MenuItemLevels.IsChecked && !String.IsNullOrEmpty(MediaBrowserContext.CopyItemProperties.LevelsLayer))
                {
                    resultList.Add("Kontrast, Gamma und Farben");
                    mustRedraw = true;
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("LEVELS");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("LEVELS", mItem.Layers.Count);

                    layer.Action = MediaBrowserContext.CopyItemProperties.LevelsLayer;
                }

                if (this.MenuItemZoom.IsChecked && !String.IsNullOrEmpty(MediaBrowserContext.CopyItemProperties.ZoomLayer))
                {
                    resultList.Add("Größe und Position");
                    mustRedraw = true;
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("ZOOM");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("ZOOM", mItem.Layers.Count);

                    layer.Action = MediaBrowserContext.CopyItemProperties.ZoomLayer;
                }

                if (this.MenuItemFlip.IsChecked && !String.IsNullOrEmpty(MediaBrowserContext.CopyItemProperties.FlipLayer))
                {
                    resultList.Add("Spiegeln");
                    mustRedraw = true;
                    MediaBrowser4.Objects.Layer layer = mItem.FindLayer("FLIP");

                    if (layer == null)
                        layer = mItem.AddDefaultLayer("FLIP", mItem.Layers.Count);

                    layer.Action = MediaBrowserContext.CopyItemProperties.FlipLayer;
                }
            }

            if (mustRedraw)
                MediaBrowserContext.SetLayersForMediaItems(list);

            if (this.PasteAction != null)
            {
                this.PasteAction.Invoke(this, new CopyPropertiesArgs() { Message = String.Join(", ", resultList), MustRedraw = mustRedraw });
            }
        }
    }
}
