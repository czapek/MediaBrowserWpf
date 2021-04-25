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
using MediaBrowserWPF.Utilities;
using System.Threading;
using System.IO;
using MediaBrowser4.Objects;
using MediaBrowser4;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using MediaProcessing;
using System.Security;
using System.Security.Permissions;
using System.Security.AccessControl;

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für CopySdCardDialog.xaml
    /// </summary>    
    public partial class CopySdCardDialog : Window
    {

        public event EventHandler<EventArgs> OnCopied;
        double act1, act2;
        enum SdType { Panasonic, Canon }

        public SortedDictionary<string, CopyItem> CopyDictionary { get; set; }
        public List<string> NotCopiedList { get; set; }
        public List<string> DirectoryList;

        public List<MediaItem> NewMediaItems
        {
            get;
            private set;
        }

        public string DrainFolder
        {
            get;
            private set;
        }

        private int FolderCnt
        {
            get;
            set;
        }

        public CopySdCardDialog()
        {
            InitializeComponent();
            this.FolderAutoCompleter.Text = MediaBrowserContext.GetDBProperty("DefaultMediaTempFolder");

            if (!Directory.Exists(this.FolderAutoCompleter.Text))
                this.FolderAutoCompleter.Text = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);

            this.Init();

            ((System.ComponentModel.INotifyPropertyChanged)Col1).PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "ActualWidth")
                {
                    act1 = Col1.ActualWidth;
                    this.Width = act1 + act2 + 80;
                }
            };

            ((System.ComponentModel.INotifyPropertyChanged)Col2).PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "ActualWidth")
                {
                    act2 = Col2.ActualWidth;
                    this.Width = act1 + act2 + 80;
                }
            };
        }

        private void Init()
        {
            if (!Directory.Exists(this.FolderAutoCompleter.Text))
                return;

            this.DrainFolder = this.FolderAutoCompleter.Text;

            if (!Directory.Exists(DrainFolder))
            {
                DrainFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }

            this.FolderCnt = 0;
            this.CopyDictionary = new SortedDictionary<string, CopyItem>();
            this.NotCopiedList = new List<string>();

            this.MediaSource.Items.Add("");

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Removable)
                {
                    this.MediaSource.Items.Add(new { Name = String.Format("{0} ({1}) {2:n1} GB", drive.VolumeLabel, drive.Name, drive.TotalSize / 1024.0 / 1024.0 / 1024.0), Value = drive });

                    if ((this.NotCopiedList.Count + this.CopyDictionary.Count) == 0 && Directory.Exists(drive.Name + "DCIM"))
                    {
                        this.MediaSource.SelectedIndex = this.MediaSource.Items.Count - 1;
                    }
                }
            }

            if (Directory.Exists(@"P:\sebastian.czapek\huawei"))
            {
                string sourceFolder = @"P:\sebastian.czapek\huawei";
                this.MediaSource.Items.Add(new { Name = String.Format("{0}", "Huawei"), Value = sourceFolder });
                this.MediaSource.SelectedIndex = this.MediaSource.Items.Count - 1;
            }

            if (Directory.Exists(@"D:\photo\import"))
            {
                string sourceFolder = @"D:\photo\import";
                this.MediaSource.Items.Add(new { Name = String.Format("{0}", "photo\\import"), Value = sourceFolder });
                this.MediaSource.SelectedIndex = this.MediaSource.Items.Count - 1;
            }
        }

        private void SetData()
        {
            this.CopyGrid.DataContext = this.CopyDictionary.Values.ToArray();
            this.NoCopyGrid.DataContext = this.NotCopiedList;
            this.CategoryNoCopy.Header = this.NotCopiedList.Count + " sonstige Dateien";
            this.CategoryCopy.Header = this.CopyDictionary.Count + " Medien-Dateien";
            this.NewMediaItems = new List<MediaItem>();


            if (this.NotCopiedList.Count == (this.NotCopiedList.Count(x => x.EndsWith(".ctg", StringComparison.InvariantCultureIgnoreCase)) +
                 this.NotCopiedList.Count(x => x.EndsWith(".tbl", StringComparison.InvariantCultureIgnoreCase)) +
                 this.NotCopiedList.Count(x => x.EndsWith(".bdm", StringComparison.InvariantCultureIgnoreCase)) +
                 this.NotCopiedList.Count(x => x.EndsWith(".tid", StringComparison.InvariantCultureIgnoreCase)) +
                 this.NotCopiedList.Count(x => x.EndsWith(".mpl", StringComparison.InvariantCultureIgnoreCase)) +
                 this.NotCopiedList.Count(x => x.EndsWith(".cpi", StringComparison.InvariantCultureIgnoreCase)) +
                 this.NotCopiedList.Count(x => x.EndsWith(".vpl", StringComparison.InvariantCultureIgnoreCase)) +
                 this.NotCopiedList.Count(x => x.EndsWith(".tdt", StringComparison.InvariantCultureIgnoreCase)))
            )
            {
                this.checkDeleteOther.IsChecked = true;
            }

            if (this.CopyDictionary.Count > 0)
            {
                this.SetSelectedThumbnail(this.CopyDictionary.Values.First(x => true).From);
                CheckCopyLog();
            }
        }

        private void MediaSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.CopyDictionary = new SortedDictionary<string, CopyItem>();
            this.NotCopiedList = new List<string>();
            this.DirectoryList = new List<string>();

            if (this.MediaSource.SelectedValue is DriveInfo)
            {
                this.AddFiles(this.MediaSource.SelectedValue as DriveInfo);
            }
            else
            {
                string sourceFolder = this.MediaSource.SelectedValue as String;
                if (Directory.Exists(sourceFolder))
                {
                    string resultDrainFolder = DrainFolder + "\\" + DateTime.Now.ToString("yyMMdd-HHmm-ss");
                    AddFiles(sourceFolder, resultDrainFolder);
                }
            }

            SetData();

            if (!(this.MediaSource.SelectedValue is DriveInfo))
            {
                checkDeleteOther.IsChecked = false;
                checkBoxDelete.IsChecked = true;
            }
            else
            {
                checkBoxDelete.IsChecked = false;
            }
        }

        private void CheckCopyLog()
        {
            int cnt = 0;
            foreach (var copyItemGroup in this.CopyDictionary.Values.GroupBy(x => System.IO.Path.GetPathRoot(x.From)))
            {
                string logFileName = System.IO.Path.Combine(copyItemGroup.Key, "MediaBrowserWpfCopyLog_" + System.Environment.MachineName + ".log");

                if (System.IO.File.Exists(logFileName))
                {
                    String[] lastCopiedFiles = System.IO.File.ReadAllLines(logFileName);
                    foreach (CopyItem copyItem in copyItemGroup)
                    {
                        if (lastCopiedFiles.Any(x => x == copyItem.From.Substring(copyItemGroup.Key.Length)))
                        {
                            copyItem.IsCopy = false;
                            checkBoxDelete.IsChecked = false;

                            cnt++;
                        }
                    }
                }
            }

            if (cnt > 0)
            {
                this.LabelInfoCopy.Text = $"{cnt:n0} Medien der Liste wurden bereits von dieser Karte auf den Rechner {System.Environment.MachineName} kopiert";
                this.LabelInfoCopy.Visibility = Visibility.Visible;
            }
            else
            {
                this.LabelInfoCopy.Visibility = Visibility.Collapsed;
            }

        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, string> copyErrors = new Dictionary<string, string>();
            MainWindow.BussyIndicatorContent = "Kopiere Dateien ...";
            MainWindow.BussyIndicatorIsBusy = true;
            bool rename = checkBoxRename.IsChecked.Value;
            bool delete = checkBoxDelete.IsChecked.Value;
            bool deleteOther = checkDeleteOther.IsChecked.Value;
            bool writeChecked = false;
            bool noWriteAccess = false;

            Thread thread = new Thread(() =>
            {
                Dictionary<string, List<string>> copiedItems = new Dictionary<string, List<string>>();
                int cnt = 0;
                foreach (CopyItem kv in this.CopyGrid.Items)
                {
                    if (!kv.IsCopy)
                        continue;

                    try
                    {
                        if (delete && !writeChecked)
                        {
                            try
                            {
                                File.WriteAllText(System.IO.Path.GetDirectoryName(kv.From) + "\\test.tmp", "A");
                            }
                            catch (Exception)
                            {
                                noWriteAccess = true;
                                break;
                            }

                            File.Delete(System.IO.Path.GetDirectoryName(kv.From) + "\\test.tmp");
                            writeChecked = true;
                        }

                        if (!Directory.Exists(System.IO.Path.GetDirectoryName(kv.To)))
                        {
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(kv.To));
                        }

                        if (delete)
                            File.Move(kv.From, kv.To);
                        else
                            File.Copy(kv.From, kv.To);

                        string root = System.IO.Path.GetPathRoot(kv.From);
                        if (!copiedItems.ContainsKey(root))
                        {
                            copiedItems[root] = new List<string>();
                        }

                        copiedItems[root].Add(kv.From.Substring(root.Length));

                        MediaItem mItem = MediaBrowserContext.GetMediaItemFromFile(kv.To);

                        if (mItem != null)
                        {
                            if (rename)
                            {
                                if (mItem.MediaDate == DateTime.MinValue)
                                {
                                    mItem.MetaData = MetaDataList.GetList(MediaProcessing.ImageExif.GetAllTags(mItem.FileObject.FullName), "MDEX");
                                    MediaBrowser4.Utilities.DateAndTime.GetMediaDate(mItem, MediaBrowserContext.MediaDateDefaultFormatString);
                                }

                                string newFilename = System.IO.Path.Combine(
                                    System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem))
                                    + System.IO.Path.GetExtension(kv.To.ToLower());

                                if (File.Exists(newFilename))
                                {
                                    int cntFile = 0;

                                    string alternativFilename = System.IO.Path.Combine(
                                            System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem))
                                            + "_" + cntFile.ToString().PadLeft(3, '0')
                                            + System.IO.Path.GetExtension(kv.To.ToLower());

                                    while (File.Exists(alternativFilename))
                                    {
                                        cntFile++;

                                        alternativFilename = System.IO.Path.Combine(
                                            System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem))
                                            + "_" + cntFile.ToString().PadLeft(3, '0')
                                            + System.IO.Path.GetExtension(kv.To.ToLower());
                                    }

                                    File.Move(newFilename, alternativFilename);
                                    MediaItem mItem2 = this.NewMediaItems.FirstOrDefault(x => x.FileObject.FullName == newFilename);
                                    if (mItem2 != null)
                                    {
                                        mItem2.Rename(alternativFilename);
                                    }

                                    cntFile++;

                                    alternativFilename = System.IO.Path.Combine(
                                            System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem))
                                            + "_" + cntFile.ToString().PadLeft(3, '0')
                                            + System.IO.Path.GetExtension(kv.To.ToLower());

                                    while (File.Exists(alternativFilename))
                                    {
                                        cntFile++;

                                        alternativFilename = System.IO.Path.Combine(
                                            System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem))
                                            + "_" + cntFile.ToString().PadLeft(3, '0')
                                            + System.IO.Path.GetExtension(kv.To.ToLower());
                                    }

                                    File.Move(kv.To, alternativFilename);
                                    mItem.Rename(alternativFilename);
                                }
                                else
                                {
                                    if (Directory.GetFiles(
                                        System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem)
                                        + "_*" + System.IO.Path.GetExtension(kv.To.ToLower())).Length == 0)
                                    {
                                        File.Move(kv.To, newFilename);
                                        mItem.Rename(newFilename);
                                    }
                                    else
                                    {
                                        int cntFile = 0;

                                        string alternativFilename = System.IO.Path.Combine(
                                                System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem))
                                                + "_" + cntFile.ToString().PadLeft(3, '0')
                                                + System.IO.Path.GetExtension(kv.To.ToLower());

                                        while (File.Exists(alternativFilename))
                                        {
                                            cntFile++;

                                            alternativFilename = System.IO.Path.Combine(
                                                System.IO.Path.GetDirectoryName(kv.To), this.NewName(mItem))
                                                + "_" + cntFile.ToString().PadLeft(3, '0')
                                                + System.IO.Path.GetExtension(kv.To.ToLower());
                                        }

                                        File.Move(kv.To, alternativFilename);
                                        mItem.Rename(alternativFilename);
                                    }
                                }
                            }
                            this.NewMediaItems.Add(mItem);
                        }

                        cnt++;
                        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                        {
                            MainWindow.BussyIndicatorContent = String.Format("Kopiere {0} / {1}", cnt, this.CopyDictionary.Count);
                        }));
                    }
                    catch (Exception ex)
                    {
                        copyErrors.Add(kv.From, ex.Message);
                    }
                }

                foreach (string key in copiedItems.Keys)
                {
                    StringBuilder sb = new StringBuilder();
                    string logFileName = System.IO.Path.Combine(key, "MediaBrowserWpfCopyLog_" + System.Environment.MachineName + ".log");
                    List<string> lastCopiedFiles = System.IO.File.Exists(logFileName) ? System.IO.File.ReadAllLines(logFileName).ToList() : new List<string>();
                    List<string> copyLog = new List<string>();

                    foreach (CopyItem kv in this.CopyGrid.Items)
                    {
                        if (kv.From.StartsWith(key) && lastCopiedFiles.Contains(kv.From.Substring(key.Length)))
                        {
                            copyLog.Add(kv.From.Substring(key.Length));
                        }
                    }

                    foreach (string value in copiedItems[key])
                    {
                        if (!copyLog.Contains(value))
                            copyLog.Add(value);
                    }

                    try
                    {
                        System.IO.File.WriteAllLines(logFileName, copyLog);
                    }
                    catch { }
                }

                if (deleteOther && this.DirectoryList != null && !noWriteAccess)
                {
                    foreach (string file in this.NotCopiedList)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { }
                    }

                    this.DirectoryList.Sort();
                    this.DirectoryList.Reverse();

                    foreach (string directory in this.DirectoryList)
                    {
                        try
                        {
                            if (Directory.GetFiles(directory).Length == 0
                                && !System.IO.Path.GetPathRoot(directory).Equals(directory))
                                Directory.Delete(directory);
                        }
                        catch { }
                    }
                }

                Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    MainWindow.BussyIndicatorIsBusy = false;

                    if (noWriteAccess)
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Es wurden keine Dateien kopiert.",
                              "Kein Schreibzugriff auf den Datenträger!", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else if (copyErrors.Count > 0)
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,
                            copyErrors.Count + " von " + this.CopyDictionary.Count + " Dateien wurden nicht kopiert.\r\n\r\n"
                            + string.Join("\r\n", copyErrors.Values.Distinct()),
                                "Beim Kopieren sind Fehler aufgetreten", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    if (this.OnCopied != null)
                        this.OnCopied.Invoke(this, EventArgs.Empty);
                }));
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            this.Close();
        }

        private string NewName(MediaItem mItem)
        {
            return mItem.MediaDate.ToString("yyMMdd-HHmm-ss") + (mItem.Filename.StartsWith("ST") ? "_panorama" : "");
        }

        private void AddFiles(DriveInfo drive)
        {
            if (drive == null)
                return;

            string sourceFolder = drive.Name;
            string resultDrainFolder = DrainFolder + "\\"
                + (drive.VolumeLabel.Length == 0 ? string.Empty : drive.VolumeLabel + "_")
                + DateTime.Now.ToString("yyMMdd-HHmm-ss");

            AddFiles(sourceFolder, resultDrainFolder);
        }

        private void AddFiles(string sourceFolder, string resultDrainFolder)
        {
            this.DirectoryList = new List<string>();
            ScanDirs(DirectoryList, sourceFolder);
            CopyItem cp;
            int currentCount = this.CopyDictionary.Count;

            foreach (string directory in DirectoryList)
            {
                if (System.IO.Path.GetFileName(directory).StartsWith("."))
                    continue;

                currentCount = this.CopyDictionary.Count;
                bool copy = System.IO.Path.GetPathRoot(directory) != directory;
                foreach (string source in Directory.GetFiles(directory))
                {
                    if (source.Contains("THMBNL"))
                        continue;

                    string folderIdentifier = "_" + this.FolderCnt.ToString().PadLeft(4, '0');


                    if (System.IO.Path.GetExtension(source).ToLower() == ".jpg")
                    {
                        string wav = System.IO.Path.GetDirectoryName(source) + "\\" + System.IO.Path.GetFileNameWithoutExtension(source).Replace("IMG", "SND") + ".WAV";
                        if (File.Exists(wav))
                        {
                            cp = new CopyItem()
                            {
                                IsCopy = copy,
                                To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(wav),
                                From = wav
                            };
                            this.CopyDictionary.Add(cp.To, cp);
                        }

                        cp = new CopyItem()
                        {
                            IsCopy = copy,
                            To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                            From = source
                        };
                        this.CopyDictionary.Add(cp.To, cp);
                    }
                    else if (System.IO.Path.GetExtension(source).ToLower() == ".avi")
                    {
                        cp = new CopyItem()
                        {
                            IsCopy = copy,
                            To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                            From = source
                        };
                        this.CopyDictionary.Add(cp.To, cp);

                    }
                    else if (System.IO.Path.GetExtension(source).ToLower() == ".mov")
                    {
                        cp = new CopyItem()
                        {
                            IsCopy = copy,
                            To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                            From = source
                        };
                        this.CopyDictionary.Add(cp.To, cp);

                    }
                    else if (System.IO.Path.GetExtension(source).ToLower() == ".thm")
                    {
                        cp = new CopyItem()
                        {
                            IsCopy = copy,
                            To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                            From = source
                        };
                        this.CopyDictionary.Add(cp.To, cp);

                    }
                    else if (System.IO.Path.GetExtension(source).ToLower() == ".wav")
                    {
                        string img = System.IO.Path.GetDirectoryName(source) + "\\" + System.IO.Path.GetFileNameWithoutExtension(source).Replace("SND", "IMG") + ".JPG";
                        if (!File.Exists(img))
                        {
                            cp = new CopyItem()
                            {
                                IsCopy = copy,
                                To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                                From = source
                            };
                            this.CopyDictionary.Add(cp.To, cp);
                        }
                    }
                    else if (System.IO.Path.GetExtension(source).ToLower() == ".mts")
                    {
                        cp = new CopyItem()
                        {
                            IsCopy = copy,
                            To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                            From = source
                        };
                        this.CopyDictionary.Add(cp.To, cp);
                    }
                    else if (System.IO.Path.GetExtension(source).ToLower() == ".mp4")
                    {
                        cp = new CopyItem()
                        {
                            IsCopy = copy,
                            To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                            From = source
                        };
                        this.CopyDictionary.Add(cp.To, cp);
                    }
                    else if (System.IO.Path.GetExtension(source).ToLower() == ".gif")
                    {
                        cp = new CopyItem()
                        {
                            IsCopy = copy,
                            To = resultDrainFolder + folderIdentifier + "\\" + System.IO.Path.GetFileName(source),
                            From = source
                        };
                        this.CopyDictionary.Add(cp.To, cp);
                    }
                    else
                    {
                        this.NotCopiedList.Add(source);

                    }
                }

                if (currentCount != this.CopyDictionary.Count)
                    FolderCnt++;
            }
        }

        private void ScanDirs(List<string> directoryList, string root)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(root))
                {
                    ScanDirs(directoryList, dir);
                }
                directoryList.Add(root);
            }
            catch
            {

            }
        }

        private void ButtonFolderDialog_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = this.FolderAutoCompleter.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.FolderAutoCompleter.Text = dialog.SelectedPath;
                this.Init();
            }
        }

        private void CopyGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                SetSelectedThumbnail(((CopyItem)e.AddedItems[0]).From);
            }
        }

        private void SetSelectedThumbnail(string path)
        {
            ImageExif imageExif = ImageExif.ImageExifFactory(path);
            if (imageExif == null)
                return;


            int[] thumbnailData = imageExif.GetThumbnailData();
            DateTime? date = imageExif.GetCreateDate();

            if (date != null)
            {
                this.LabelExifDate.Content = "Erstellt am " + date.Value.ToString("f");
            }
            else
            {
                this.LabelExifDate.Content = "";
            }

            if (thumbnailData.Length > 0)
            {
                byte[] thumbnailDatab = thumbnailData.Select(p => (byte)p).ToArray();

                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(thumbnailDatab);
                bi.EndInit();

                this.ThumbnailImage.Source = bi;
            }
            else
            {
                this.ThumbnailImage.Source = null;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            foreach (CopyItem kv in this.CopyGrid.SelectedItems)
            {
                kv.IsCopy = true;
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            foreach (CopyItem kv in this.CopyGrid.SelectedItems)
            {
                kv.IsCopy = false;
            }
        }
    }

    public class CopyItem : INotifyPropertyChanged
    {
        public string From { get; set; }
        public string To { get; set; }

        bool isCopy;
        public bool IsCopy { get { return this.isCopy; } set { this.isCopy = value; this.OnPropertyChanged("IsCopy"); } }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
