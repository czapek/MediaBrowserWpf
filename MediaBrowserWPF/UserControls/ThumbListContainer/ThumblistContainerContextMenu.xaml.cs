using MediaBrowser4;
using MediaBrowser4.Objects;
using MediaBrowser4.Utilities;
using MediaBrowserWPF.Dialogs;
using MediaBrowserWPF.Utilities;
using MediaProcessing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using FilesAndFolders = MediaBrowserWPF.Utilities.FilesAndFolders;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für ThumblistContainerContextMenu.xaml
    /// </summary>
    public partial class ThumblistContainerContextMenu : ContextMenu
    {
        ThumblistContainer thumblistContainer;

        public ThumblistContainerContextMenu()
        {
            InitializeComponent();
        }

        public ThumblistContainer ThumblistContainer
        {
            set
            {
                this.CopyMenuItem.MediaItemList = value.SelectedMediaItems;
                this.thumblistContainer = value;
            }

            get
            {
                return this.thumblistContainer;
            }
        }

        private void CategorizeMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.CategorizeMenuItem.MediaItemList = this.CopyMenuItem.MediaItemList;
            Mouse.OverrideCursor = null;
        }

        public void Copy(List<MediaItem> list)
        {
            this.CopyMenuItem.Copy(list);
        }

        public void Paste(List<MediaItem> list)
        {
            this.CopyMenuItem.Paste(list);
        }

        public void SetSortTypeList(List<Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection>> sortTypeList, MediaItemRequestShuffleType shuffleType)
        {
            this.MenuItemSortFolder.IsChecked = false;
            this.MenuItemSortMediadate.IsChecked = false;
            this.MenuItemSortFilename.IsChecked = false;
            this.MenuItemSortType.IsChecked = false;
            this.MenuItemSortLength.IsChecked = false;
            this.MenuItemSortDuration.IsChecked = false;
            this.MenuItemSortViewed.IsChecked = false;
            this.MenuItemSortArea.IsChecked = false;
            this.MenuItemSortRelation.IsChecked = false;
            this.MenuItemSortShuffle.IsChecked = false;
            this.MenuItemSortShuffle5.IsChecked = false;
            this.MenuItemSortShuffleMedia.IsChecked = false;
            this.MenuItemSortShuffleCategoryDay.IsChecked = false;

            MediaItemRequestSortDirection sortDirection = MediaItemRequestSortDirection.ASCENDING;

            foreach (Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection> tupel in sortTypeList)
            {
                sortDirection = tupel.Item2;

                switch (tupel.Item1)
                {
                    case MediaItemRequestSortType.AREA:
                        this.MenuItemSortArea.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.DURATION:
                        this.MenuItemSortDuration.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.FILENAME:
                        this.MenuItemSortFilename.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.FOLDERNAME:
                        this.MenuItemSortFolder.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.LENGTH:
                        this.MenuItemSortLength.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.MEDIADATE:
                        this.MenuItemSortMediadate.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.MEDIATYPE:
                        this.MenuItemSortType.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.RELATION:
                        this.MenuItemSortRelation.IsChecked = true;
                        break;

                    case MediaItemRequestSortType.VIEWED:
                        this.MenuItemSortViewed.IsChecked = true;
                        break;
                }
            }

            this.MenuItemSortAsc.IsChecked = this.MenuItemSortDesc.IsChecked = this.MenuItemSortShuffleMedia.IsChecked
                = this.MenuItemSortShuffle5.IsChecked = this.MenuItemSortShuffle.IsChecked = this.MenuItemSortShuffleCategoryDay.IsChecked = false;

            if (shuffleType == MediaItemRequestShuffleType.NONE)
            {
                this.MenuItemSortAsc.IsChecked = (sortDirection == MediaItemRequestSortDirection.ASCENDING);
                this.MenuItemSortDesc.IsChecked = (sortDirection == MediaItemRequestSortDirection.DESCENDING);
            }
            else
            {
                this.MenuItemSortShuffle5.IsChecked = (shuffleType == MediaItemRequestShuffleType.SHUFFLE_5);
                this.MenuItemSortShuffleMedia.IsChecked = (shuffleType == MediaItemRequestShuffleType.SHUFFLE_MEDIA);
                this.MenuItemSortShuffle.IsChecked = (shuffleType == MediaItemRequestShuffleType.SHUFFLE);
                this.MenuItemSortShuffleCategoryDay.IsChecked = (shuffleType == MediaItemRequestShuffleType.SHUFFLE_MEDIADATE_DAY);
            }
        }

        public void UncheckAllSortTypes()
        {
            this.MenuItemSortArea.IsChecked = false;
            this.MenuItemSortDuration.IsChecked = false;
            this.MenuItemSortFilename.IsChecked = false;
            this.MenuItemSortFolder.IsChecked = false;
            this.MenuItemSortLength.IsChecked = false;
            this.MenuItemSortMediadate.IsChecked = false;
            this.MenuItemSortType.IsChecked = false;
            this.MenuItemSortRelation.IsChecked = false;
            this.MenuItemSortViewed.IsChecked = false;
            this.UncheckBaseSortType();
        }

        public void UncheckBaseSortType()
        {
            this.MenuItemSortShuffle.IsChecked = false;
            this.MenuItemSortShuffle5.IsChecked = false;
            this.MenuItemSortShuffleMedia.IsChecked = false;
            this.MenuItemSortAsc.IsChecked = false;
            this.MenuItemSortDesc.IsChecked = false;
            this.MenuItemSortShuffleCategoryDay.IsChecked = false;
        }

        private void MenuItem_Sort_Asc(object sender, RoutedEventArgs e)
        {
            UncheckBaseSortType();
            this.MenuItemSortAsc.IsChecked = true;
            this.Sort(MediaItemRequestSortDirection.ASCENDING);
        }

        private void MenuItem_Sort_Desc(object sender, RoutedEventArgs e)
        {
            UncheckBaseSortType();
            this.MenuItemSortDesc.IsChecked = true;
            this.Sort(MediaItemRequestSortDirection.DESCENDING);
        }

        private void MenuItemSortShuffle_Click(object sender, RoutedEventArgs e)
        {
            UncheckBaseSortType();
            this.MenuItemSortShuffle.IsChecked = true;
            this.Sort(MediaItemRequestSortDirection.ASCENDING);
        }

        private void MenuItemSortShuffle5_Click(object sender, RoutedEventArgs e)
        {
            UncheckBaseSortType();
            this.MenuItemSortShuffle5.IsChecked = true;
            this.Sort(MediaItemRequestSortDirection.ASCENDING);
        }

        private void MenuItemSortShuffleCategoryDay_Click(object sender, RoutedEventArgs e)
        {
            UncheckBaseSortType();
            this.MenuItemSortShuffleCategoryDay.IsChecked = true;
            this.Sort(MediaItemRequestSortDirection.ASCENDING);
        }

        private void MenuItemSortShuffleMedia_Click(object sender, RoutedEventArgs e)
        {
            UncheckBaseSortType();
            this.MenuItemSortShuffleMedia.IsChecked = true;
            this.Sort(MediaItemRequestSortDirection.ASCENDING);
        }

        private void MenuItem_Sort_UncheckAll(object sender, RoutedEventArgs e)
        {
            bool start = false;
            foreach (Object item in this.MenuItemSort.Items)
            {
                if (item is Separator)
                {
                    start = !start;
                }
                else if (start)
                {
                    ((MenuItem)item).IsChecked = false;
                }
            }

            this.MenuItemSortCritera.IsChecked = false;
        }

        public void Sort(MediaItemRequestSortDirection sortDirection)
        {
            this.thumblistContainer.Request.SortTypeList.Clear();

            if (this.MenuItemSortFolder.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.FOLDERNAME, sortDirection));

            if (this.MenuItemSortMediadate.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIADATE, sortDirection));

            if (this.MenuItemSortFilename.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.FILENAME, sortDirection));

            if (this.MenuItemSortType.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.MEDIATYPE, sortDirection));

            if (this.MenuItemSortLength.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.LENGTH, sortDirection));

            if (this.MenuItemSortDuration.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.DURATION, sortDirection));

            if (this.MenuItemSortViewed.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.VIEWED, sortDirection));

            if (this.MenuItemSortArea.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.AREA, sortDirection));

            if (this.MenuItemSortRelation.IsChecked)
                this.thumblistContainer.Request.SortTypeList.Add(Tuple.Create(MediaItemRequestSortType.RELATION, sortDirection));

            if (this.MenuItemSortCritera.IsChecked)
            {
                Tuple<MediaItemRequestSortType, MediaItemRequestSortDirection>[] tupleArray = this.thumblistContainer.Request.SortTypeList.ToArray();
                this.thumblistContainer.Request.SortTypeList.Clear();
                for (int i = tupleArray.Length - 1; i >= 0; i--)
                {
                    this.thumblistContainer.Request.SortTypeList.Add(tupleArray[i]);
                }
            }

            if (this.MenuItemSortShuffle.IsChecked)
            {
                this.thumblistContainer.Request.ShuffleType = MediaItemRequestShuffleType.SHUFFLE;
            }
            else if (this.MenuItemSortShuffle5.IsChecked)
            {
                this.thumblistContainer.Request.ShuffleType = MediaItemRequestShuffleType.SHUFFLE_5;
            }
            else if (this.MenuItemSortShuffleMedia.IsChecked)
            {
                this.thumblistContainer.Request.ShuffleType = MediaItemRequestShuffleType.SHUFFLE_MEDIA;
            }
            else if (this.MenuItemSortShuffleCategoryDay.IsChecked)
            {
                this.thumblistContainer.Request.ShuffleType = MediaItemRequestShuffleType.SHUFFLE_MEDIADATE_DAY;
            }
            else
            {
                this.thumblistContainer.Request.ShuffleType = MediaItemRequestShuffleType.NONE;
            }

            this.thumblistContainer.SortMediaItemList();
        }

        private void MenuItemExportChecksumList_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem == null)
                return;

            System.Windows.Forms.SaveFileDialog fDlg = new System.Windows.Forms.SaveFileDialog();

            fDlg.Filter = "Textdatei (*.hash)|*.hash|Alle Formate (*.*)|*.*";
            fDlg.FilterIndex = 0;
            fDlg.FileName = "MediaBrowser4ChecksumList.hash";
            fDlg.InitialDirectory = MediaBrowserContext.GetDBProperty("DefaultMediaArchivFolder");
            fDlg.Title = "Erstellt eine Textdatei mit den Prüfsummen der Originaldateien";

            if (fDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();

                foreach (MediaItem mItem in this.thumblistContainer.SelectedMediaItems)
                {
                    sb.AppendLine(mItem.Md5Value);
                }

                System.IO.File.WriteAllText(fDlg.FileName, sb.ToString(), Encoding.Default);
            }
        }

        private void MenuItemExportNameCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem == null)
                return;

            String result = String.Join(",", this.thumblistContainer.SelectedMediaItems
                .Select(x => Path.GetFileNameWithoutExtension(x.Filename)));

            Clipboard.SetText(result);
        }

        private void MenuItemExportMediaList_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem == null)
                return;

            System.Windows.Forms.SaveFileDialog fDlg = new System.Windows.Forms.SaveFileDialog();

            fDlg.Filter = "Textdatei (*.txt)|*.txt|Alle Formate (*.*)|*.*";
            fDlg.FilterIndex = 0;
            fDlg.FileName = "MediaBrowser4Playlist.txt";
            fDlg.InitialDirectory = MediaBrowserContext.GetDBProperty("DefaultMediaArchivFolder");
            fDlg.Title = "Erstellt eine Textdatei mit absoluten Dateipfaden";

            if (fDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();

                foreach (MediaItem mItem in this.thumblistContainer.SelectedMediaItems)
                {
                    sb.AppendLine(mItem.FileObject.FullName);
                }

                System.IO.File.WriteAllText(fDlg.FileName, sb.ToString(), Encoding.Default);
            }
        }

        private void MenuItemExportPlayList_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem == null)
                return;

            System.Windows.Forms.SaveFileDialog fDlg = new System.Windows.Forms.SaveFileDialog();

            fDlg.Filter = "Playlist (*.m3u)|*.m3u|Alle Formate (*.*)|*.*";
            fDlg.FilterIndex = 0;
            fDlg.FileName = "MediaBrowser4Playlist.m3u";
            fDlg.InitialDirectory = this.thumblistContainer.SelectedMediaItem.FileObject.DirectoryName;
            fDlg.Title = "Erstellt eine M3U-Liste mit relativen Dateipfaden";

            if (fDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();

                foreach (MediaItem mItem in this.thumblistContainer.SelectedMediaItems)
                {
                    sb.AppendLine(mItem.FileObject.FullName);
                }

                System.IO.File.WriteAllText(fDlg.FileName, sb.ToString(), Encoding.Default);
            }
        }

        private void MenuItemBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ToggleBookmark();
        }

        private void MenuItemLocalCache_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.SendToLokalCache();
        }

        private void MenuItemMarkDeleted_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ToggleMarkDelete();
        }

        private void MenuItemOpenWith_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.OpenWith();
        }

        private void MenuItemOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.OpenInExplorer();
        }

        private void MenuItemExport800_Click(object sender, RoutedEventArgs e)
        {
            this.ExportImage(800, false);
        }

        private void MenuItemPreviewDbExport_Click(object sender, RoutedEventArgs e)
        {
            List<int> previewDbVariationIdList = MediaBrowserContext.GetPreviewDBVariationIdList();
            this.FFMpegScript(new System.Drawing.Size(640, 480), 100, previewDbVariationIdList);
            this.ExportImage(800, false, true, previewDbVariationIdList);
        }

        private void MenuItemExport1024_Click(object sender, RoutedEventArgs e)
        {
            this.ExportImage(1024, false);
        }

        private void MenuItemExport3840_Click(object sender, RoutedEventArgs e)
        {
            this.ExportImage(3840, false);
        }

        private void MenuItemExport10000_Click(object sender, RoutedEventArgs e)
        {
            this.ExportImage(10000, false);
            this.ExportImage(800, false, "thumbs");
        }

        private void MenuItemExportThumbnails_Click(object sender, RoutedEventArgs e)
        {
            this.ExportImage(exportSize: 150, false);
        }

        private void MenuItemExportFramsung0_Click(object sender, RoutedEventArgs e)
        {
            ExportFramsung(0.0);
        }

        private void MenuItemExportFramsung25_Click(object sender, RoutedEventArgs e)
        {
            ExportFramsung(0.25);
        }

        private void MenuItemExportFramsung50_Click(object sender, RoutedEventArgs e)
        {
            ExportFramsung(0.5);
        }

        private void MenuItemExportFramsung75_Click(object sender, RoutedEventArgs e)
        {
            ExportFramsung(0.75);
        }

        private void MenuItemExportFramsung100_Click(object sender, RoutedEventArgs e)
        {
            ExportFramsung(1.0);
        }

        private void ExportFramsung(double relforcedPos)
        {
            using (TakeSnapshot takeSnapshot = new TakeSnapshot() { OverwriteExisting = true })
            {
                takeSnapshot.ExportImage(
                    this.thumblistContainer.SelectedMediaItems.Where(x => x.AspectRatioCropped > 0 && x is MediaItemBitmap && MediaBrowserContext.GetVariations(x).Count == 1).OrderBy(x => x.FileObject.Name).ToList(),
                    7680,
                    this.RelativeImageBorder,
                    this.ImageRelation,
                    this.ImageQuality,
                    this.SharpenQuality,
                    false,
                    MenuItemExportImageOptionsFullName.IsChecked,
                    false, null, this.MenuItemExportImageOptionsForceCrop.IsChecked, 4320, relforcedPos);
            }
        }

        private void MenuItemExport1920_Click(object sender, RoutedEventArgs e)
        {
            this.ExportImage(1920, false);
        }

        private void MenuItemExportLigtbox_Click(object sender, RoutedEventArgs e)
        {
            this.ExportImage(1024, true);
        }

        private void ExportImage(double exportSize, bool lightbox, String subfolder)
        {
            using (TakeSnapshot takeSnapshot = new TakeSnapshot() { Subfolder = subfolder })
            {
                takeSnapshot.ExportImage(
                    this.thumblistContainer.SelectedMediaItems.OrderBy(x => x.FileObject.Name).ToList(),
                    exportSize,
                this.RelativeImageBorder,
                    this.ImageRelation,
                    this.ImageQuality,
                    this.SharpenQuality,
                    lightbox,
                MenuItemExportImageOptionsFullName.IsChecked,
                    false, null, this.MenuItemExportImageOptionsForceCrop.IsChecked);
            }
        }

        private void ExportImage(double exportSize, bool lightbox)
        {
            this.ExportImage(exportSize, lightbox, false, null);
        }

        private void ExportImage(double exportSize, bool lightbox, bool previewDb, List<int> previewDbVariationIdList)
        {
            using (TakeSnapshot takeSnapshot = new TakeSnapshot())
            {
                takeSnapshot.ExportImage(
                    this.thumblistContainer.SelectedMediaItems.OrderBy(x => x.FileObject.Name).ToList(),
                    exportSize,
                    this.RelativeImageBorder,
                    this.ImageRelation,
                    previewDb ? 65 : this.ImageQuality,
                    this.SharpenQuality,
                    lightbox,
                    MenuItemExportImageOptionsFullName.IsChecked,
                    previewDb, previewDbVariationIdList, this.MenuItemExportImageOptionsForceCrop.IsChecked);
            }
        }

        private double ImageRelation
        {
            get
            {
                if (this.MenuItemExportImageOptionsBorder11.IsChecked)
                    return 1.0;
                else if (this.MenuItemExportImageOptionsBorder32.IsChecked)
                    return 3.0 / 2.0;
                else if (this.MenuItemExportImageOptionsBorder43.IsChecked)
                    return 4.0 / 3.0;
                else if (this.MenuItemExportImageOptionsBorder169.IsChecked)
                    return 16.0 / 9.0;

                return 0.0;
            }
        }

        private double RelativeImageBorder
        {
            get
            {
                if (this.MenuItemExportImageOptionsBorder05.IsChecked)
                    return 0.5;
                else if (this.MenuItemExportImageOptionsBorder1.IsChecked)
                    return 1.0;
                else if (this.MenuItemExportImageOptionsBorder2.IsChecked)
                    return 2.0;
                else if (this.MenuItemExportImageOptionsBorder4.IsChecked)
                    return 4.0;

                return 0.0;
            }
        }

        private int ImageQuality
        {
            get
            {
                if (this.MenuItemExportImageOptionsQuality65.IsChecked)
                    return 65;
                else if (this.MenuItemExportImageOptionsQuality75.IsChecked)
                    return 75;
                else if (this.MenuItemExportImageOptionsQuality100.IsChecked)
                    return 100;

                return 90;
            }
        }

        private SharpenImage.Quality SharpenQuality
        {
            get
            {
                if (this.MenuItemExportImageOptionsSharpenSoft.IsChecked)
                    return SharpenImage.Quality.SOFT;
                else if (this.MenuItemExportImageOptionsSharpenMedium.IsChecked)
                    return SharpenImage.Quality.MEDIUM;
                else if (this.MenuItemExportImageOptionsSharpenStrong.IsChecked)
                    return SharpenImage.Quality.STRONG;

                return SharpenImage.Quality.NONE;
            }
        }

        private void MenuItemOpenDbTree_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ShowInDbTree();
        }

        private void MenuItemSelectDublicates_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.SupernummerousDublicates(MediaItem.DublicateCriteria.CHECKSUM);
            Mouse.OverrideCursor = null;
        }

        private void MenuItemSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.thumblistContainer.SelectFolder();
            Mouse.OverrideCursor = null;
        }

        private void MenuItemSelectInvert_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.thumblistContainer.InvertSelection();
            Mouse.OverrideCursor = null;
        }

        private void MenuItemSelect50Next_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.thumblistContainer.Next50Selection();
            Mouse.OverrideCursor = null;
        }

        private delegate string DublicatesHandler(MediaItem mItem);
        private void SupernummerousDublicates(MediaItem.DublicateCriteria criteria)
        {
            this.ThumblistContainer.ShowAllMediaItems();

            DublicatesHandler dublicatesHandler = null;

            switch (criteria)
            {
                case MediaItem.DublicateCriteria.CONTAINS:
                case MediaItem.DublicateCriteria.FILENAME:
                    dublicatesHandler = delegate (MediaItem mItem) { return mItem.Filename.ToLower(); };
                    break;

                case MediaItem.DublicateCriteria.CHECKSUM:
                    dublicatesHandler = delegate (MediaItem mItem) { return mItem.Md5Value; };
                    break;

                default:
                    throw new Exception("DublicateCriteria not found");

            }

            foreach (MediaItem mItem in thumblistContainer.MediaItemList)
                mItem.IsSelected = false;

            Dictionary<string, List<MediaItem>> dic = new Dictionary<string, List<MediaItem>>();
            foreach (MediaItem mItem in thumblistContainer.MediaItemList)
            {
                if (dic.ContainsKey(dublicatesHandler(mItem)))
                {
                    dic[dublicatesHandler(mItem)].Add(mItem);
                }
                else
                {
                    dic.Add(dublicatesHandler(mItem), new List<MediaItem>() { mItem });
                }

                if (mItem.IsDublicate(criteria) || mItem.ThumbJpegData == null)
                    mItem.IsSelected = true;
                else
                    mItem.IsSelected = false;
            }


            List<MediaItem> unselect = new List<MediaItem>();
            foreach (MediaItem mItem in thumblistContainer.MediaItemList)
            {
                List<int> dubIDs = new List<int>(
                    MediaBrowserContext.GetDublicates(mItem, criteria).Keys);

                if (dubIDs.Count == 0)
                    unselect.Add(mItem);

                if (dic.ContainsKey(mItem.Md5Value) &&
                    dic[mItem.Md5Value].Count > 1)
                {
                    bool notInList = true;
                    foreach (int id in dubIDs)
                    {
                        notInList = true;
                        foreach (MediaItem mItem2 in thumblistContainer.MediaItemList)
                        {
                            if (mItem2.Id == id)
                            {
                                notInList = false;
                                break;
                            }
                        }

                        if (notInList)
                        {
                            break;
                        }
                    }

                    if (!notInList)
                    {
                        unselect.Add(mItem);
                        dic.Remove(mItem.Md5Value);
                    }
                }
            }

            foreach (MediaItem mItem in unselect)
            {
                mItem.IsSelected = false;
            }

            this.ThumblistContainer.Select(thumblistContainer.MediaItemList.Where(x => x.IsSelected).ToList());

            // MediaBrowserContext.SetDeleted(thumblistContainer.MediaItemList.Where(x => x.IsSelected).ToList(), true);
        }

        private void MenuItemDeleteFromDb_Click(object sender, RoutedEventArgs e)
        {
            this.ThumblistContainer.RemoveFromDb();
        }

        private void MenuItemRemoveTemp_Click(object sender, RoutedEventArgs e)
        {
            this.ThumblistContainer.RemoveTemp();
        }

        private void MenuItemSelectDeleted_Click(object sender, RoutedEventArgs e)
        {
            this.ThumblistContainer.SelectDeleted();
        }

        private void MenuItemFFMpegH264_NoResize_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(0, 0), 0);
        }

        private void MenuItemFFMpegH264_1280x720_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(1280, 720), 0);
        }

        private void MenuItemFFMpegH264_1920x1080_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(1920, 1080), 0);
        }

        private void MenuItemFFMpegXVid_NoResize_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(0, 0), 1);
        }

        private void MenuItemFFMpegH265_NoResize_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(0, 0), 8);
        }

        private void MenuItemFFMpegH265_1920x1080_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(1920, 1080), 8);
        }

        private void MenuItemFFMpegH265_3840x2160_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(3840, 2160), 8);
        }

        private void MenuItemFFMpegXVid_640x480_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(640, 480), 1);
        }

        private void MenuItemFFMpeg_Mp3_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(0, 0), 200);
        }

        private void MenuItemFFMpegOggTheora_NoResize_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(150, 150), 3);
        }

        private void MenuItemFFMpeg_Copy_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(0, 0), 4);
        }

        private void MenuItemFFMpegMpeg2_720x57_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(720, 576), 5);
        }

        private void MenuItemFFMpegImage25_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(0, 0), 6);
        }

        private void MenuItemFFMpegWebm_NoREsize_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(0, 0), 7);
        }

        private void MenuItemFFMpegWebm_1920x1080_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(1920, 1080), 7);
        }

        private void MenuItemFFMpegWebm_3840x2160_Click(object sender, RoutedEventArgs e)
        {
            this.FFMpegScript(new System.Drawing.Size(3840, 2160), 7);
        }

        private void FFMpegScript(System.Drawing.Size size, int index)
        {
            this.FFMpegScript(size, index, null);
        }

        private void MenuItemMKVMerge_Join_Click(object sender, RoutedEventArgs e)
        {
            string mkvMergePath = MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("MKVToolNix", "mkvmerge.exe");
            List<MediaItem> videoItems = thumblistContainer.SelectedMediaItems.Where(x => x is MediaItemVideo).ToList();

            if (!String.IsNullOrWhiteSpace(mkvMergePath) && videoItems.Count > 1)
            {
                bool start = true;
                StringBuilder mkvBatch = new StringBuilder();
                string exportPath = MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder();

                foreach (MediaItem mItem in videoItems.OrderBy(x => x.FullName))
                {
                    if (start)
                    {
                        mkvBatch.Append(String.Format("\"{0}\" -o \"{1}\" \"{2}\"", mkvMergePath, exportPath + "\\"
                            + System.IO.Path.GetFileNameWithoutExtension(videoItems[0].Filename) + "_joined.mkv", mItem.FullName));

                        start = false;
                    }
                    else
                    {
                        mkvBatch.Append(String.Format(" +\"{0}\"", mItem.FullName));
                    }
                }

                string skriptPath = exportPath + "\\mkvJoin.bat";
                System.IO.File.WriteAllText(skriptPath, mkvBatch.ToString(), Encoding.Default);
                MediaBrowserWPF.Utilities.FilesAndFolders.OpenExplorer(skriptPath, true);
                Process p = new Process();
                p.StartInfo.FileName = skriptPath;
                p.Start();

            }
        }

        private void FFMpegScript(System.Drawing.Size size, int index, List<int> previewDbVariationIdList)
        {
            Createffmpeg createffmpeg = new Createffmpeg();
            Process p = new Process();

            if (index == 100)
            {
                createffmpeg.PreviewDbVariationIdList = previewDbVariationIdList;
                index = 6;
                p.EnableRaisingEvents = true;
                p.Exited += p_Exited;
                createffmpeg.IsPreviewDb = true;
                createffmpeg.ExportPath = MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopPreviewDbFolder();
            }
            else
            {
                createffmpeg.ExportPath = MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopExportFolder();
            }

            createffmpeg.SetByPredefinedValue(index);//isH264 ? (size.Width == 480 ? 2 : 0) : 1);
            createffmpeg.VideoSize = size;
            createffmpeg.SampleLength = size.Width == 150 ? 10 : 0;
            string skriptPath = createffmpeg.Start(this.ThumblistContainer.SelectedMediaItems);

            if (skriptPath != null && File.Exists(createffmpeg.FFmpegPath))
            {
                MediaBrowserWPF.Utilities.FilesAndFolders.OpenExplorer(skriptPath, true);
                p.StartInfo.FileName = skriptPath;
                p.Start();
            }
        }

        void p_Exited(object sender, EventArgs e)
        {
            string exportPath = MediaBrowserWPF.Utilities.FilesAndFolders.CreateDesktopPreviewDbFolder();

            MainWindow.CreateContactprintFromThumbs(exportPath);
        }


        private void MenuItemVariationsNew_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
                (new Dialogs.VariationDialog(this.thumblistContainer.SelectedMediaItems, Dialogs.VariationDialog.EditorType.NEW) { Owner = MainWindow.MainWindowStatic }).ShowDialog();
        }

        private void MenuItemVariationsDelete_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
                (new Dialogs.VariationDialog(this.thumblistContainer.SelectedMediaItems, Dialogs.VariationDialog.EditorType.DELETE) { Owner = MainWindow.MainWindowStatic }).ShowDialog();
        }

        private void MenuItemVariationsRename_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
                (new Dialogs.VariationDialog(this.thumblistContainer.SelectedMediaItems, Dialogs.VariationDialog.EditorType.RENAME) { Owner = MainWindow.MainWindowStatic }).ShowDialog();
        }

        private void MenuItemVariationsDefault_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
                (new Dialogs.VariationDialog(this.thumblistContainer.SelectedMediaItems, Dialogs.VariationDialog.EditorType.SETDEFAULT) { Owner = MainWindow.MainWindowStatic }).ShowDialog();
        }

        private void MenuItemVariationsComplete_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
                (new Dialogs.VariationDialog(this.thumblistContainer.SelectedMediaItems, Dialogs.VariationDialog.EditorType.COMPLETE) { Owner = MainWindow.MainWindowStatic }).ShowDialog();
        }

        private void MenuItemOpenMultiplayer_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ShowMultiplayer();
        }

        private void MenuItemOpenMultiplayerXxX_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            if (menuItem != null)
            {
                string a = menuItem.Name.Substring(23);
                int colCount = Int32.Parse(a.Split('x')[0]);
                int rowCount = Int32.Parse(a.Split('x')[1]);

                this.thumblistContainer.ShowMultiplayer(colCount, rowCount);

            }
        }

        private void MenuItemOpenPlayerIntern_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ShowMediaItem();
        }

        private void MenuItemOpen3DCube_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ShowCube3D();
        }

        private void MenuItemOpen3DSphere_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ShowSphere3D();
        }

        private void MenuItemExportXmlFull_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItems.Count == 0)
                return;

            System.Windows.Forms.SaveFileDialog saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();

            saveFileDialog1.Filter = "XML File (*.xml)|*.xml";
            saveFileDialog1.FileName = "MediaBrowser2Export.xml";
            saveFileDialog1.FilterIndex = 0;
            saveFileDialog1.RestoreDirectory = false;
            saveFileDialog1.InitialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);

            if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MainWindow.BussyIndicatorIsBusy = true;

                Thread thread = new Thread(() =>
                {
                    XmlMetadata xml = new XmlMetadata();
                    xml.ExportMessage += new EventHandler(xml_NextItem);
                    XmlDocument xmlDoc = xml.Export(this.thumblistContainer.SelectedMediaItems, true);

                    XmlTextWriter writer = new XmlTextWriter(saveFileDialog1.FileName, Encoding.UTF8);
                    writer.Formatting = Formatting.Indented;
                    xmlDoc.Save(writer);
                    writer.Close();

                    Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                    {
                        MainWindow.BussyIndicatorIsBusy = false;
                    }));
                });

                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        void xml_NextItem(object sender, EventArgs e)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                MainWindow.BussyIndicatorContent = ((XmlMetadata)sender).Message;
            }));
        }

        private void MenuItemExportImageOptionsBorder2_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (!this.MenuItemExportImageOptionsBorder05.IsChecked
                && !this.MenuItemExportImageOptionsBorder1.IsChecked
                && !this.MenuItemExportImageOptionsBorder2.IsChecked
                && !this.MenuItemExportImageOptionsBorder4.IsChecked)
                this.MenuItemExportImageOptionsForceCrop.IsChecked = true;

            this.MenuItemExportImageOptionsBorder11.IsChecked = this.MenuItemExportImageOptionsBorder11 == sender;
            this.MenuItemExportImageOptionsBorder169.IsChecked = this.MenuItemExportImageOptionsBorder169 == sender;
            this.MenuItemExportImageOptionsBorder43.IsChecked = this.MenuItemExportImageOptionsBorder43 == sender;
            this.MenuItemExportImageOptionsBorder32.IsChecked = this.MenuItemExportImageOptionsBorder32 == sender;
        }

        private void MenuItemExportImageOptionsBorder_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            this.MenuItemExportImageOptionsBorder05.IsChecked = this.MenuItemExportImageOptionsBorder05 == sender;
            this.MenuItemExportImageOptionsBorder1.IsChecked = this.MenuItemExportImageOptionsBorder1 == sender;
            this.MenuItemExportImageOptionsBorder2.IsChecked = this.MenuItemExportImageOptionsBorder2 == sender;
            this.MenuItemExportImageOptionsBorder4.IsChecked = this.MenuItemExportImageOptionsBorder4 == sender;
        }

        private void MenuItemExportImageOptionsForceCrop_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (!this.MenuItemExportImageOptionsBorder11.IsChecked
                && !this.MenuItemExportImageOptionsBorder169.IsChecked
                && !this.MenuItemExportImageOptionsBorder43.IsChecked
                && !this.MenuItemExportImageOptionsBorder32.IsChecked)
                this.MenuItemExportImageOptionsBorder169.IsChecked = true;
        }

        private void MenuItemExportImageOptionsBorder_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (!this.MenuItemExportImageOptionsBorder05.IsChecked
             && !this.MenuItemExportImageOptionsBorder1.IsChecked
             && !this.MenuItemExportImageOptionsBorder2.IsChecked
             && !this.MenuItemExportImageOptionsBorder4.IsChecked
             && !this.MenuItemExportImageOptionsForceCrop.IsChecked)
            {
                this.MenuItemExportImageOptionsBorder11.IsChecked = false;
                this.MenuItemExportImageOptionsBorder169.IsChecked = false;
                this.MenuItemExportImageOptionsBorder43.IsChecked = false;
                this.MenuItemExportImageOptionsBorder32.IsChecked = false;
            }
        }

        private void MenuItemExportImageOptionsQuality_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            this.MenuItemExportImageOptionsQuality65.IsChecked = this.MenuItemExportImageOptionsQuality65 == sender;
            this.MenuItemExportImageOptionsQuality75.IsChecked = this.MenuItemExportImageOptionsQuality75 == sender;
            this.MenuItemExportImageOptionsQuality90.IsChecked = this.MenuItemExportImageOptionsQuality90 == sender;
            this.MenuItemExportImageOptionsQuality100.IsChecked = this.MenuItemExportImageOptionsQuality100 == sender;
        }

        private void MenuItemExportImageOptionsQuality_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (!this.MenuItemExportImageOptionsQuality65.IsChecked
              && !this.MenuItemExportImageOptionsQuality75.IsChecked
              && !this.MenuItemExportImageOptionsQuality90.IsChecked
              && !this.MenuItemExportImageOptionsQuality100.IsChecked)
            {
                this.MenuItemExportImageOptionsQuality90.IsChecked = true;
            }
        }

        private void MenuItemExportImageOptionsSharpen_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            this.MenuItemExportImageOptionsSharpenSoft.IsChecked = this.MenuItemExportImageOptionsSharpenSoft == sender;
            this.MenuItemExportImageOptionsSharpenMedium.IsChecked = this.MenuItemExportImageOptionsSharpenMedium == sender;
            this.MenuItemExportImageOptionsSharpenStrong.IsChecked = this.MenuItemExportImageOptionsSharpenStrong == sender;
        }

        private void MenuItemExportImageOptionsBorder11_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded)
                return;

            if (!this.MenuItemExportImageOptionsBorder11.IsChecked
                && !this.MenuItemExportImageOptionsBorder169.IsChecked
                && !this.MenuItemExportImageOptionsBorder43.IsChecked
                && !this.MenuItemExportImageOptionsBorder32.IsChecked)
                this.MenuItemExportImageOptionsForceCrop.IsChecked = false;
        }

        private void MenuItemPreviewDbDelete_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            MediaBrowserContext.DeleteFromPreviewDB(this.thumblistContainer.SelectedMediaItems);
            Mouse.OverrideCursor = null;
        }

        private void MenuItemRenameSmart_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ShowRenameDialog();
        }

        private void MenuItemSelectMonthNext_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.SelectByTimeSpan(new ThumblistContainer.GetTimeSpanName(MediaBrowser4.Utilities.DateAndTime.GetMonthName));
        }


        private void MenuItemSelectWeekNext_Click(object sender, RoutedEventArgs e)
        {
            string selectedWeek = this.thumblistContainer.SelectByTimeSpan(new ThumblistContainer.GetTimeSpanName(MediaBrowser4.Utilities.DateAndTime.GetWeekName));

            if (selectedWeek != null && MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(MediaBrowserContext.DefaultMediaArchivRoot) != null)
            {
                string folderParentPath = System.IO.Path.Combine(MediaBrowserContext.DefaultMediaArchivRoot, this.ThumblistContainer.SelectedMediaItem.MediaDate.Year.ToString());
                string folderWeekPath = System.IO.Path.Combine(folderParentPath, selectedWeek);
                Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(folderWeekPath);

                if (folder == null)
                {
                    Folder folderParent = MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(folderParentPath);

                    if (folderParent != null)
                    {
                        if (!Directory.Exists(folderWeekPath))
                        {
                            Directory.CreateDirectory(folderWeekPath);
                        }

                        folder = new Folder(MediaBrowserContext.FolderTreeSingelton);

                        folder.Parent = folderParent;
                        folder.Name = selectedWeek;

                        MediaBrowserContext.SetFolder(folder);
                        MediaBrowserContext.FolderTreeSingelton.FullFolderCollection.Add(folder);

                    }
                }

                if (folder != null)
                {
                    MainWindow.MainWindowStatic.TabControlControls.SelectedItem = MainWindow.MainWindowStatic.FolderTab;
                    //MainWindow.MainWindowStatic.DataBaseFolderTree.CollapseAll();
                    MainWindow.MainWindowStatic.DataBaseFolderTree.ExpandPath(folder.FullPath);

                    this.thumblistContainer.CutToClipboard();
                }
            }
        }

        private void MenuItemSelectDayNext_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.SelectByTimeSpan(new ThumblistContainer.GetTimeSpanName(MediaBrowser4.Utilities.DateAndTime.GetDayName));
        }

        private void MenuItemSelectHourNext_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.SelectByTimeSpan(new ThumblistContainer.GetTimeSpanName(MediaBrowser4.Utilities.DateAndTime.GetHourName));
        }

        private void MenuItemOpenWithPanorama_Click(object sender, RoutedEventArgs e)
        {
            PanoramaStudio("ms");
        }

        private void MenuItemFaceDetectionSelect_Click(object sender, RoutedEventArgs e)
        {
            double minSizeFactor = 0;
            double maxSizeFactor = 100;
            int minCnt = 1;
            int maxCnt = 1000;

            if (this.MenuItemFaceDetectionPortraitTiny.IsChecked
                 && this.MenuItemFaceDetectionPortraitBig.IsChecked)
            {
                this.MenuItemFaceDetectionPortraitSmall.IsChecked = true;
            }

            if (this.MenuItemFaceDetectionPortraitTiny.IsChecked
                 && !this.MenuItemFaceDetectionPortraitSmall.IsChecked
                 && !this.MenuItemFaceDetectionPortraitBig.IsChecked)
            {
                minSizeFactor = 0;
                maxSizeFactor = 2.5;
            }
            else if (!this.MenuItemFaceDetectionPortraitTiny.IsChecked
                 && this.MenuItemFaceDetectionPortraitSmall.IsChecked
                 && !this.MenuItemFaceDetectionPortraitBig.IsChecked)
            {
                minSizeFactor = 2.5;
                maxSizeFactor = 7;
            }
            else if (!this.MenuItemFaceDetectionPortraitTiny.IsChecked
               && !this.MenuItemFaceDetectionPortraitSmall.IsChecked
               && this.MenuItemFaceDetectionPortraitBig.IsChecked)
            {
                minSizeFactor = 7;
                maxSizeFactor = 100;
            }
            else if (this.MenuItemFaceDetectionPortraitTiny.IsChecked
               && this.MenuItemFaceDetectionPortraitSmall.IsChecked
               && !this.MenuItemFaceDetectionPortraitBig.IsChecked)
            {
                minSizeFactor = 0;
                maxSizeFactor = 7;
            }
            else if (!this.MenuItemFaceDetectionPortraitTiny.IsChecked
               && this.MenuItemFaceDetectionPortraitSmall.IsChecked
               && this.MenuItemFaceDetectionPortraitBig.IsChecked)
            {
                minSizeFactor = 2.5;
                maxSizeFactor = 100;
            }

            if (this.MenuItemFaceDetectionSingle.IsChecked
                && this.MenuItemFaceDetectionBigGroup.IsChecked)
            {
                this.MenuItemFaceDetectionHandsfull.IsChecked = true;
            }

            if (this.MenuItemFaceDetectionSingle.IsChecked
                && !this.MenuItemFaceDetectionHandsfull.IsChecked
                && !this.MenuItemFaceDetectionBigGroup.IsChecked)
            {
                minCnt = 1;
                maxCnt = 1;
            }
            else if (!this.MenuItemFaceDetectionSingle.IsChecked
             && this.MenuItemFaceDetectionHandsfull.IsChecked
             && !this.MenuItemFaceDetectionBigGroup.IsChecked)
            {
                minCnt = 2;
                maxCnt = 5;
            }
            else if (!this.MenuItemFaceDetectionSingle.IsChecked
             && !this.MenuItemFaceDetectionHandsfull.IsChecked
             && this.MenuItemFaceDetectionBigGroup.IsChecked)
            {
                minCnt = 6;
                maxCnt = 1000;
            }
            else if (this.MenuItemFaceDetectionSingle.IsChecked
             && this.MenuItemFaceDetectionHandsfull.IsChecked
             && !this.MenuItemFaceDetectionBigGroup.IsChecked)
            {
                minCnt = 1;
                maxCnt = 5;
            }
            else if (!this.MenuItemFaceDetectionSingle.IsChecked
             && this.MenuItemFaceDetectionHandsfull.IsChecked
             && this.MenuItemFaceDetectionBigGroup.IsChecked)
            {
                minCnt = 2;
                maxCnt = 1000;
            }

            FindPortraits(minSizeFactor, maxSizeFactor, minCnt, maxCnt);
        }

        private void FindPortraits(double minSizeFactor, double maxSizeFactor, int minCnt, int maxCnt)
        {
            Dictionary<int, int> resultList = MediaBrowserContext.GetAllFaces(minSizeFactor, maxSizeFactor, minCnt, maxCnt, thumblistContainer.MediaItemList);

            thumblistContainer.SelectMediaItems(
                thumblistContainer.MediaItemList.Where(x => resultList.ContainsKey(x.VariationId)).ToList()
                );
        }

        private void MenuItemFaceDetectionShow_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).ShowSelectedImage(true, true, thumblistContainer.SelectedMediaItem);
        }

        private void PanoramaStudio(string key)
        {
            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                string path = MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("PanoramaStudio3Pro", "PanoramaStudio3Pro.exe");

                //path = MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("Hugin", "bin\\hugin.exe");

                try
                {
                    if (!String.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                    {
                        Clipboard.SetText(System.IO.Path.GetFileNameWithoutExtension(this.thumblistContainer.SelectedMediaItems[0].Filename));
                        System.Diagnostics.Process.Start(path, " -" + key + " \"" + String.Join("\" \"", this.thumblistContainer.SelectedMediaItems.OrderBy(x => x.Filename).Select(x => x.FullName)) + "\"");
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {

                }
            }
        }

        private void ICE()
        {
            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                string path = MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("Microsoft Research", "Image Composite Editor\\ICE.exe");

                try
                {
                    if (!String.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                    {
                        Clipboard.SetText(System.IO.Path.GetFileNameWithoutExtension(this.thumblistContainer.SelectedMediaItems[0].Filename));
                        System.Diagnostics.Process.Start(path, "\"" + String.Join("\" \"", this.thumblistContainer.SelectedMediaItems.OrderBy(x => x.Filename).Select(x => x.FullName)) + "\"");
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {

                }
            }
        }

        private void Hugin()
        {
            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                string path = MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("Hugin", "bin\\hugin.exe");

                try
                {
                    if (!String.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                    {
                        Clipboard.SetText(System.IO.Path.GetFileNameWithoutExtension(this.thumblistContainer.SelectedMediaItems[0].Filename));
                        System.Diagnostics.Process.Start(path, "\"" + String.Join("\" \"", this.thumblistContainer.SelectedMediaItems.OrderBy(x => x.Filename).Select(x => x.FullName)) + "\"");
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {

                }
            }
        }

        private void MenuItemOpenWithICE_Click(object sender, RoutedEventArgs e)
        {
            ICE();
        }

        private void MenuItemOpenWithHugin_Click(object sender, RoutedEventArgs e)
        {
            Hugin();
        }

        private void MenuItemOpenWithPanoramaMehrreihig_Click(object sender, RoutedEventArgs e)
        {
            PanoramaStudio("mm");
        }

        private void MenuItemOpenWithPanoramaDocument_Click(object sender, RoutedEventArgs e)
        {
            PanoramaStudio("md");
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            string pathPanoramaStudio = MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("PanoramaStudio3Pro", "PanoramaStudio3Pro.exe");
            string pathHugin = null;//MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("Hugin", "bin\\hugin.exe");
            string pathICE = MediaBrowserWPF.Utilities.FilesAndFolders.FindApplication("Microsoft Research", "Image Composite Editor\\ICE.exe");

            this.MenuItemOpenWithICE.Visibility = !String.IsNullOrWhiteSpace(pathICE) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(pathICE)) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            this.MenuItemOpenWithICE.IsEnabled = this.thumblistContainer.SelectedMediaItems.Count > 1;

            this.MenuItemOpenWithHugin.Visibility = !String.IsNullOrWhiteSpace(pathHugin) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(pathHugin)) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            this.MenuItemOpenWithHugin.IsEnabled = this.thumblistContainer.SelectedMediaItems.Count > 1;

            this.MenuItemPanoramaStudio.Visibility = !String.IsNullOrWhiteSpace(pathPanoramaStudio) && System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(pathPanoramaStudio)) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            this.MenuItemPanoramaStudio.IsEnabled = this.thumblistContainer.SelectedMediaItems.Count > 1;

            this.MenuItemOpenDublicate.Visibility = (this.thumblistContainer.SelectedMediaItems.Count == 1
                && this.thumblistContainer.SelectedMediaItems[0].IsMd5Dublicate) ? Visibility.Visible : Visibility.Collapsed;

            // this.MenuItemGeo.Visibility = MediaBrowserContext.HasGeodata ? Visibility.Visible : Visibility.Collapsed;

        }

        private void MenuItemOpenPreviewWindow_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).ShowSelectedImage(true, false, thumblistContainer.SelectedMediaItem);
        }

        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;
        private void MenuItemOpenDublicate_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItems.Count == 1)
            {
                SearchToken searchToken = new SearchToken()
                {
                    SearchText1 = this.thumblistContainer.SelectedMediaItems[0].Md5Value,
                    SearchText1Description = false,
                    SearchText1Category = false,
                    SearchText1Filename = false,
                    SearchText1Folder = false,
                    SearchText1Not = false,
                    SearchText1Md5 = true
                };

                if (!searchToken.IsValid)
                    return;

                MediaItemSearchRequest searchRequest = new MediaItemSearchRequest(searchToken, this.thumblistContainer.SelectedMediaItems[0].Md5Value.GetHashCode())
                {
                    UserDefinedName = "Dubletten"
                };

                if (this.OnRequest != null)
                {
                    this.OnRequest(this, new MediaItemRequestMessageArgs(searchRequest));
                }
            }
        }

        private void MenuItemRedateToSummerTime_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.AdjustMediaDate(this.thumblistContainer.SelectedMediaItems.Select(x => { x.MediaDate = x.MediaDate.AddHours(-1); return x; }).ToList());
        }

        private void MenuItemRedateToWinterTime_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.AdjustMediaDate(this.thumblistContainer.SelectedMediaItems.Select(x => { x.MediaDate = x.MediaDate.AddHours(1); return x; }).ToList());
        }

        private void MenuItemAdjustExifTime_Click(object sender, RoutedEventArgs e)
        {
            MediaBrowserWPF.UserControls.ThumbListContainer.AdjustMediaDateDialog inputDialog = new MediaBrowserWPF.UserControls.ThumbListContainer.AdjustMediaDateDialog("Das neue Datum im passenden Format eingeben:");
            if (inputDialog.ShowDialog() == true)
            {
                try
                {
                    MediaBrowserContext.AdjustMediaDate(this.thumblistContainer.SelectedMediaItems.Select(x => { x.MediaDate = inputDialog.AnswerDate; return x; }).ToList());
                }
                catch
                {
                }
            }
        }

        private void MenuItemUpdateGeodata_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            foreach (MediaItem mItem in this.thumblistContainer.SelectedMediaItems)
            {
                using (Stream stream = new System.IO.FileStream(mItem.FileObject.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {


                    BitmapDecoder decoder;
                    switch (Path.GetExtension(mItem.Filename).ToLower())
                    {
                        case ".jpg":
                        case ".jpeg":
                            decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                            break;
                        case ".png":
                            decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                            break;
                        default:
                            throw new Exception();
                    }
                    //135375
                    BitmapFrame frame = decoder.Frames[0];

                    InPlaceBitmapMetadataWriter inplace = frame.CreateInPlaceBitmapMetadataWriter();
                    inplace.SetQuery("/Text/Description", "Das merkwürdige ist nur, dass der laptop mit 100% ausgeschaltet wird und am nächsten Tag mit 100% wieder hoch fährt.\r\nAb und an ist es auch so,dass wenn er am am netzteil ist und auf 100% geladen ist und ich ihn dann weiter nutze mit Netzteil er auf 90% runter geht..");


                    if (inplace.TrySave() == true)
                    {
                        inplace.SetQuery("/Text/Description", "Das merkwürdige ist nur, dass der laptop mit 100% ausgeschaltet wird und am nächsten Tag mit 100% wieder hoch fährt.\r\nAb und an ist es auch so,dass wenn er am am netzteil ist und auf 100% geladen ist und ich ihn dann weiter nutze mit Netzteil er auf 90% runter geht..");
                    }
                }
            }
            /**
            foreach (MediaItem mItem in this.thumblistContainer.SelectedMediaItems.Where(x => !x.Latitude.HasValue))
            {
                GeoPoint gps = MediaBrowserContext.GetGpsNearest(mItem.MediaDate);
                if (gps != null)
                {
                    mItem.Longitude = gps.Longitude;
                    mItem.Latitude = gps.Latitude;
                }
            }

            MediaBrowserContext.SetGeodata(this.thumblistContainer.SelectedMediaItems.Where(x => x.Latitude.HasValue).ToList());
            */
            Mouse.OverrideCursor = null;
        }


        private void MenuItemUpdateGeodataDialog_Click(object sender, RoutedEventArgs e)
        {
            MapControl.MapSearchWindow sc = new MapControl.MapSearchWindow(this.thumblistContainer.SelectedMediaItems);
            sc.Owner = MainWindow.MainWindowStatic;
            sc.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            sc.ShowDialog();
        }

        private void MenuItemOpenGeoNearest_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
            {
                GeoPoint gps = null;
                if (this.thumblistContainer.SelectedMediaItem.Latitude.HasValue)
                    gps = new GeoPoint() { Latitude = this.thumblistContainer.SelectedMediaItem.Latitude.Value, Longitude = this.thumblistContainer.SelectedMediaItem.Longitude.Value };
                else
                    gps = MediaBrowserContext.GetGpsNearest(this.thumblistContainer.SelectedMediaItem.MediaDate);

                if (gps != null)
                {
                    this.thumblistContainer.SelectedMediaItem.Longitude = gps.Longitude;
                    this.thumblistContainer.SelectedMediaItem.Latitude = gps.Latitude;
                    MediaBrowserContext.SetGeodata(new List<MediaItem>() { this.thumblistContainer.SelectedMediaItem });
                    string url = $"https://www.google.com/maps/place/{gps.Latitude}+{gps.Longitude}/@{gps.Latitude},{gps.Longitude},15z&language=de".Replace(",", ".").Replace(" ", ",");
                    System.Diagnostics.Process.Start(url);
                }
            }
        }

        private void MenuItemGoogleTimeline_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
            {
                DateTime mediaDate = this.thumblistContainer.SelectedMediaItem.MediaDate;
                System.Diagnostics.Process.Start($"https://www.google.de/maps/timeline?hl=de&authuser=0&pb=!1m2!1m1!1s{mediaDate:yyyy}-{mediaDate:MM}-{mediaDate:dd}");

            }
        }

        private void MenuItemOpenGeoAdress_Click(object sender, RoutedEventArgs e)
        {
            this.thumblistContainer.ShowGeoAdress();
        }

        private void MenuItemExportGpxMedia_Click(object sender, RoutedEventArgs e)
        {
            string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\MediabBrowserGeoExport", this.thumblistContainer.SelectedMediaItem.MediaDate.ToString("yyyyMMdd_HHmm"));

            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                GpxHelper.ExportGpx(this.thumblistContainer.SelectedMediaItems.Min(x => x.MediaDate), this.thumblistContainer.SelectedMediaItems.Max(x => x.MediaDate), file + ".gpx");
                KmlHelper.ExportKml(this.thumblistContainer.SelectedMediaItems.Min(x => x.MediaDate), this.thumblistContainer.SelectedMediaItems.Max(x => x.MediaDate), file + ".kml");
            }
            else if (this.thumblistContainer.SelectedMediaItems.Count > 0)
            {
                GpxHelper.ExportGpx(this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddHours(3), this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddHours(27), file + ".gpx");
                KmlHelper.ExportKml(this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddHours(3), this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddHours(27), file + ".kml");
            }
        }

        private void MenuItemOpenGps_Click(object sender, RoutedEventArgs e)
        {
            int days = Int32.Parse(((MenuItem)sender).Name.Substring(21, 1));
            string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\MediabBrowserGeoExport", DateTime.Now.Date.AddDays(-days + 1).ToString("yyyyMMdd_HHmm"));
            GpxHelper.ExportGpx(DateTime.Now.Date.AddDays(-days + 1), DateTime.Now, file + ".gpx");
            KmlHelper.ExportKml(DateTime.Now.Date.AddDays(-days + 1), DateTime.Now, file + ".kml");
        }

        private void MenuItemOpenYesterday_Click(object sender, RoutedEventArgs e)
        {
            string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\MediabBrowserGeoExport", DateTime.Now.Date.AddDays(-1).ToString("yyyyMMdd_HHmm"));
            GpxHelper.ExportGpx(DateTime.Now.Date.AddDays(-1), DateTime.Now.Date, file + ".gpx");
            KmlHelper.ExportKml(DateTime.Now.Date.AddDays(-1), DateTime.Now.Date, file + ".kml");
        }

        private void MenuItemOpenGeoWeather_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItem != null)
            {
                GeoPoint gps = null;
                if (this.thumblistContainer.SelectedMediaItem.Latitude.HasValue)
                    gps = new GeoPoint() { Latitude = this.thumblistContainer.SelectedMediaItem.Latitude.Value, Longitude = this.thumblistContainer.SelectedMediaItem.Longitude.Value };
                else
                    gps = MediaBrowserContext.GetGpsNearest(this.thumblistContainer.SelectedMediaItem.MediaDate);

                if (gps != null)
                {
                    //GeoAdress geoAdress = GeoAdress.GetAdressXml(gps);

                    //if (geoAdress != null && geoAdress.PoliticalFormatted != null)
                    //{
                    //    DateTime date = this.thumblistContainer.SelectedMediaItem.MediaDate;
                    //    string name = HttpUtility.UrlEncode(geoAdress.PoliticalFormatted);
                    //    string url = $"https://www.wunderground.com/history/index.html?error=AMBIGUOUS&query={name}&day={date.Day}&month={date.Month}&year={date.Year}";

                    //    System.Diagnostics.Process.Start(url);
                    //}
                }
            }
        }

        private void MenuItemOpenGeoDay_Click(object sender, RoutedEventArgs e)
        {
            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                KmlHelper.OpenDay(this.thumblistContainer.SelectedMediaItems.Min(x => x.MediaDate), this.thumblistContainer.SelectedMediaItems.Max(x => x.MediaDate));
            }
            else if (this.thumblistContainer.SelectedMediaItems.Count > 0)
            {
                KmlHelper.OpenDay(this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddHours(3), this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddHours(27));
            }
        }

        private void MenuItemOpenGeo_Click(object sender, RoutedEventArgs e)
        {
            int days = Int32.Parse(((MenuItem)sender).Name.Substring(19, 1));
            KmlHelper.OpenDay(DateTime.Now.Date.AddDays(-days + 1), DateTime.Now);
        }

        private void MenuItemOpenGeoYesterday_Click(object sender, RoutedEventArgs e)
        {
            KmlHelper.OpenDay(DateTime.Now.Date.AddDays(-1), DateTime.Now.Date);
        }

        private void MenuItemOpenGeoCustom_Click(object sender, RoutedEventArgs e)
        {
            StartStopDialog dialog = null;

            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                dialog = new StartStopDialog(this.thumblistContainer.SelectedMediaItems.Min(x => x.MediaDate), this.thumblistContainer.SelectedMediaItems.Max(x => x.MediaDate));
            }
            else if (this.thumblistContainer.SelectedMediaItems.Count == 1)
            {
                dialog = new StartStopDialog(this.thumblistContainer.SelectedMediaItem.MediaDate.Date, this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddDays(1));
            }
            else
            {
                dialog = new StartStopDialog(DateTime.Now.Date, DateTime.Now.AddDays(1).Date);
            }

            dialog.Owner = MainWindow.MainWindowStatic;
            dialog.ShowDialog();

            if (dialog.DialogResult.HasValue && dialog.DialogResult.Value)
            {
                DateTime start = dialog.StartDate.HasValue ? dialog.StartDate.Value : DateTime.MinValue;
                DateTime stop = dialog.StopDate.HasValue ? dialog.StopDate.Value : DateTime.MaxValue;
                KmlHelper.OpenDay(start, stop);
            }
        }

        private void MenuItemOpenCustom_Click(object sender, RoutedEventArgs e)
        {
            StartStopDialog dialog = null;

            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                dialog = new StartStopDialog(this.thumblistContainer.SelectedMediaItems.Min(x => x.MediaDate), this.thumblistContainer.SelectedMediaItems.Max(x => x.MediaDate));
            }
            else if (this.thumblistContainer.SelectedMediaItems.Count == 1)
            {
                dialog = new StartStopDialog(this.thumblistContainer.SelectedMediaItem.MediaDate.Date, this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddDays(1));
            }
            else
            {
                dialog = new StartStopDialog(DateTime.Now.Date, DateTime.Now.AddDays(1).Date);
            }

            dialog.Owner = MainWindow.MainWindowStatic;
            dialog.ShowDialog();

            if (dialog.DialogResult.HasValue && dialog.DialogResult.Value)
            {
                DateTime start = dialog.StartDate.HasValue ? dialog.StartDate.Value : DateTime.MinValue;
                DateTime stop = dialog.StopDate.HasValue ? dialog.StopDate.Value : DateTime.MaxValue;

                string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\MediabBrowserGeoExport", start.ToString("yyyyMMdd_HHmm"));
                GpxHelper.ExportGpx(start, stop, file + ".gpx");
                KmlHelper.ExportKml(start, stop, file + ".kml");
            }
        }

        private void MenuItemDistance_Click(object sender, RoutedEventArgs e)
        {
            List<GeoPoint> geoList = null;
            if (this.thumblistContainer.SelectedMediaItems.Count > 1)
            {
                geoList = MediaBrowserContext.GetGpsList(this.thumblistContainer.SelectedMediaItems.Min(x => x.MediaDate), this.thumblistContainer.SelectedMediaItems.Max(x => x.MediaDate));
            }
            else if (this.thumblistContainer.SelectedMediaItems.Count == 1)
            {
                geoList = MediaBrowserContext.GetGpsList(this.thumblistContainer.SelectedMediaItem.MediaDate.Date, this.thumblistContainer.SelectedMediaItem.MediaDate.Date.AddDays(1));
            }
            else
            {
                geoList = MediaBrowserContext.GetGpsList(DateTime.Now.Date, DateTime.Now.AddDays(1).Date);
            }

            MessageBox.Show($"{geoList.Sum(x => x.DistanceMeter):n0} Meter");
        }

        public static void ResizeJpg(MediaItem mItem, string path, int nWidth, int nHeight)
        {
            using (var result = new Bitmap(nWidth, nHeight))
            {
                using (var input = new Bitmap(mItem.FullName))
                {
                    using (Graphics g = Graphics.FromImage((System.Drawing.Image)result))
                    {
                        g.DrawImage(input, 0, 0, nWidth, nHeight);
                    }
                }

                var ici = ImageCodecInfo.GetImageEncoders().FirstOrDefault(ie => ie.MimeType == "image/jpeg");
                var eps = new EncoderParameters(1);
                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                result.Save(path, ici, eps);
            }
        }

        private void PhotoSphereViewer_Click(object sender, RoutedEventArgs e)
        {
            string root = @"\\192.168.2.129\web\insta360";
            string url = "https://pilzchen.synology.me/insta360/";
            StringBuilder sb = new StringBuilder();
            foreach (MediaItem mitem in this.thumblistContainer.SelectedMediaItems.Where(x => x.Width > x.Height && x.Width / x.Height == 2 && x is MediaItemBitmap))
            {
                String basePath = Path.Combine(root, Path.GetFileNameWithoutExtension(mitem.Filename));
                if (Directory.Exists(basePath))
                {
                    Directory.Delete(basePath, true);
                }
                Directory.CreateDirectory(basePath);

                if (!File.Exists(Path.Combine(basePath, "image.jpg")))
                    ResizeJpg(mitem, Path.Combine(basePath, "image.jpg"), 10000, 5000);

                if (!File.Exists(Path.Combine(basePath, "preview.jpg")))
                    File.WriteAllBytes(Path.Combine(basePath, "preview.jpg"), mitem.ThumbJpegData);

                File.WriteAllText(Path.Combine(basePath, "equirectangular.html"), PhotoSphereViewer.Equirectangular);
                File.WriteAllText(Path.Combine(basePath, "fisheye.html"), PhotoSphereViewer.Fisheye);
                File.WriteAllText(Path.Combine(basePath, "original.html"), PhotoSphereViewer.Original);
                File.WriteAllText(Path.Combine(basePath, "littleplanet.html"), PhotoSphereViewer.Littleplanet);
                sb.AppendLine("<a target='_blank' href='" + Path.GetFileNameWithoutExtension(mitem.Filename) + "/fisheye.html'><img src='" + Path.GetFileNameWithoutExtension(mitem.Filename) + "/preview.jpg'></a>");
                Process.Start(url + Path.GetFileNameWithoutExtension(mitem.Filename) + "/fisheye.html");
            }
            File.WriteAllText(Path.Combine(root, "index.html"), sb.ToString());
        }



    }
}
