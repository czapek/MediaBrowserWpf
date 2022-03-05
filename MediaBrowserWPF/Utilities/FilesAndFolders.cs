using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4;
using System.Windows.Input;
using MediaBrowser4.Objects;
using System.Windows;
using System.IO;
using System.Diagnostics;
using MediaBrowser4.Utilities;

namespace MediaBrowserWPF.Utilities
{
    public static class FilesAndFolders
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void OpenExplorer(string path, bool recycle)
        {
            bool explorerExists = false;
            if (recycle)
            {
                string dirName = System.IO.Path.GetDirectoryName(path);
                if (System.IO.Directory.Exists(path))
                    dirName = path;

                dirName = dirName.Split('\\')[dirName.Split('\\').Length - 1];

                foreach (Process process in Process.GetProcesses())
                {
                    if (process.ProcessName == "explorer" && process.MainWindowTitle.EndsWith(dirName))
                    {
                        explorerExists = true;
                        SetForegroundWindow(process.MainWindowHandle);
                        break;
                    }
                }
            }

            if (!explorerExists)
            {
                if (System.IO.File.Exists(path))
                    Process.Start("explorer.exe", "/e, /select, \"" + path + "\"");
                else if (System.IO.Directory.Exists(path))
                    Process.Start("explorer.exe", "/e,\"" + path + "\"");
                else if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
                    Process.Start("explorer.exe", "/e,\"" + System.IO.Path.GetDirectoryName(path) + "\"");
            }
        }

        public static string FindApplication(string dirStartsWith, string contains)
        {
            foreach (string look in System.IO.Directory.GetDirectories(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), dirStartsWith + "*"))
            {
                if (System.IO.File.Exists(look + "\\" + contains))
                    return look + "\\" + contains;
            }

            return null;
        }

        public static void CopyRessource(string ressource, string folder)
        {
            if (!Directory.Exists(Path.GetDirectoryName(Path.Combine(folder, ressource))))
            {
                Directory.CreateDirectory((Path.GetDirectoryName(Path.Combine(folder, ressource))));
            }

            if (File.Exists(Path.Combine(System.Windows.Forms.Application.StartupPath + "\\Resources", ressource)) && !File.Exists(Path.Combine(folder, ressource)))
            {
                File.Copy(Path.Combine(System.Windows.Forms.Application.StartupPath + "\\Resources", ressource), Path.Combine(folder, ressource));
            }
        }

        public static string DesktopExportFolder
        {
            get
            {
                return System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                    "MediaBrowser " + DateTime.Now.ToString("d").Replace("/", ""));
            }
        }

        public static string DesktopPreviewDbFolder
        {
            get
            {
                return System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory),
                    "VorschauDb");
            }
        }

        public static string FindFramsungPath()
        {
            string path = null;
            String framsungPath = Path.Combine(Environment.ExpandEnvironmentVariables("%userprofile%"), @"SynologyDrive\Documents\Framsung_7680x4320\tmp");

            if (Directory.Exists(framsungPath))
            {
                path = framsungPath;

            }

            return path;
        }

        public static string CreateDesktopExportFolder()
        {
            string path = DesktopExportFolder;

            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            return path;
        }

        public static string CreateDesktopPreviewDbFolder()
        {
            string path = DesktopPreviewDbFolder;

            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            return path;
        }

        public static List<string> AddCopyMoveFromClipboard(Folder drainFolder)
        {
            List<string> clipboardList = new List<string>();

            if (Clipboard.ContainsData(DataFormats.FileDrop))
            {
                bool move = false;
                Mouse.OverrideCursor = Cursors.Wait;
                List<MediaItem> mediaList = new List<MediaItem>();
                List<string> xmlList = new List<string>();
                int newMediaItemsCount = 0;

                MemoryStream strm = Clipboard.GetData("Preferred DropEffect") as MemoryStream;

                if (strm != null)
                {
                    move = strm.ReadByte() == 2;
                }

                if (!move)
                    drainFolder = null;

                foreach (string path in (String[])Clipboard.GetData(DataFormats.FileDrop))
                {
                    if (Directory.Exists(path))
                    {
                        foreach (String file in Directory.GetFiles(path))
                        {
                            clipboardList.Add(file);
                        }
                    }

                    if (File.Exists(path))
                    {
                        clipboardList.Add(path);
                    }
                }

                if (clipboardList.Count == 0)
                {
                    Mouse.OverrideCursor = null;
                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Keine Medien ausgewählt!", "Medien verschieben", MessageBoxButton.OK, MessageBoxImage.Information);
                    return clipboardList;
                }

                foreach (string file in clipboardList)
                {
                    MediaItem mItem = MediaBrowserContext.GetMediaItemFromFullname(file);

                    if (mItem == null)
                    {
                        mItem = MediaBrowserContext.GetMediaItemFromFile(file);

                        if (mItem != null)
                        {
                            mediaList.Add(mItem);
                            newMediaItemsCount++;
                        }
                        else if (file.ToLower().EndsWith(".xml"))
                        {
                            xmlList.Add(file);
                        }
                    }
                    else if (drainFolder != null)
                    {
                        mediaList.Add(mItem);
                    }
                }
                Mouse.OverrideCursor = null;

                if (xmlList.Count > 0)
                {
                    return clipboardList;
                }

                if (newMediaItemsCount == 0 && drainFolder == null)
                {
                    Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Keine neuen Medien gefunden!", "Medien verschieben", MessageBoxButton.OK, MessageBoxImage.Information);
                    return clipboardList;
                }

                if (mediaList.Count > 0)
                {
                    string sourceFolder = String.Join("\r\n", mediaList.Select(x => x.Foldername).Distinct().OrderBy(x => x.ToLower()));

                    bool moveFiles = true;
                    MessageBoxResult result = MessageBoxResult.None;

                    if (newMediaItemsCount == mediaList.Count && drainFolder == null)
                    {
                        moveFiles = false;
                        result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, String.Format(
                          "Möchten Sie {0}\r\nzur Datenbank hinzufügen?\r\n\r\nQuelle:\r\n{1}",
                          newMediaItemsCount == 1 ? "ein neues Medium" : newMediaItemsCount + " neue Medien",
                          sourceFolder),
                          "Medien neu hinzufügen", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

                        if (result == MessageBoxResult.Yes)
                        {
                            MainWindow.AddNewMediaFiles(mediaList.Select(x => x.FileObject.FullName).ToList());
                        }
                        Mouse.OverrideCursor = null;
                        return clipboardList;
                    }

                    if (result == MessageBoxResult.None)
                    {
                        if (drainFolder == null)
                        {
                            Mouse.OverrideCursor = null;
                            Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, "Kein Ziel-Ordner ausgewählt!", "Medien verschieben", MessageBoxButton.OK, MessageBoxImage.Information);
                            return clipboardList;
                        }

                        if (newMediaItemsCount > 0)
                        {
                            result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, String.Format(
                              "Möchten Sie nach dem Hinzufügen von {0}\r\nzur Datenbank "
                              + (newMediaItemsCount == mediaList.Count ? (newMediaItemsCount == 1 ? "dieses" : "diese") : "{1}")
                              + " verschieben?\r\n\r\nZiel:\r\n{3}\r\n\r\nQuelle:\r\n{2}",
                              newMediaItemsCount == 1 ? "einem neuen Medium" : newMediaItemsCount + " neuen Medien",
                              mediaList.Count == 1 ? "ein Medium" : mediaList.Count + " Medien",
                              sourceFolder,
                              drainFolder.FullPath),
                              "Medien neu hinzufügen und verschieben", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);

                            if (result == MessageBoxResult.No)
                                moveFiles = false;
                        }
                        else
                        {
                            if (sourceFolder == drainFolder.FullPath)
                            {
                                Mouse.OverrideCursor = null;
                                return clipboardList;
                            }

                            result = Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic, String.Format(
                                "Möchten Sie {0} verschieben?\r\n\r\nZiel:\r\n{2}\r\n\r\nQuelle:\r\n{1}",
                                mediaList.Count == 1 ? "ein Medium" : mediaList.Count + " Medien",
                                sourceFolder,
                                drainFolder.FullPath),
                                "Medien verschieben", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

                            if (result == MessageBoxResult.No)
                                result = MessageBoxResult.Cancel;
                        }
                    }

                    if (result != MessageBoxResult.Cancel)
                    {
                        Mouse.OverrideCursor = Cursors.Wait;
                        try
                        {
                            List<string> newMediafileList = new List<string>();
                            List<MediaItem> copiedMediaItems = new List<MediaItem>();

                            foreach (MediaItem mItem in mediaList)
                            {

                                if (mItem.Id == 0)
                                {
                                    if (moveFiles)
                                    {
                                        if (MediaBrowser4.Utilities.FilesAndFolders.MoveMediaItem(mItem, drainFolder.FullPath) == null)
                                            newMediafileList.Add(mItem.FileObject.FullName);
                                    }
                                    else
                                        newMediafileList.Add(mItem.FileObject.FullName);
                                }
                                else
                                {
                                    if (moveFiles && MediaBrowser4.Utilities.FilesAndFolders.MoveMediaItem(mItem, drainFolder.FullPath) == null)
                                    {
                                        copiedMediaItems.Add(mItem);

                                        if (drainFolder.Id != mItem.FolderId)
                                        {
                                            MainWindow.RemoveFromFolderInThumblistContainers(mItem);
                                        }
                                    }
                                }
                            }

                            MainWindow.AddNewMediaFiles(newMediafileList);

                            if (drainFolder != null)
                            {
                                MediaBrowserContext.CopyToFolder(copiedMediaItems, drainFolder);
                                MainWindow.RefreshThumblistContainersByFolder(drainFolder);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Exception(ex);
                        }
                    }
                }
            }
            Mouse.OverrideCursor = null;

            return clipboardList;
        }
    }
}
