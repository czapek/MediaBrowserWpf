using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MediaBrowser4.Objects;
using System.ComponentModel;
using MediaBrowser4;

namespace SmartRename
{
    public enum Result { NONE, OK, FAIL, ILLEGAL_CHARACTERS }
    public class RenameFile : INotifyPropertyChanged
    {
        private string _filePath;
        public string FilePath
        {
            get { return _filePath; }
            set { _filePath = value; }
        }

        public string FullPath
        {
            get
            {
                return Path.Combine(this._filePath, this._originalName);
            }
        }

        private bool _rename = true;
        public bool Rename
        {
            get { return _rename; }
            set { this._rename = value; this.OnPropertyChanged("Rename"); }
        }

        private string _originalName;
        public string OriginalName
        {
            get { return _originalName; }
            set { _originalName = value; }
        }

        public string OriginalNameWithoutExtension
        {
            get
            {
                return Path.GetFileNameWithoutExtension(this.OriginalName);
            }
        }

        private string _newName;
        public string NewName
        {
            get { return _newName; }
            set { this._newName = value; this.OnPropertyChanged("NewName"); }
        }

        private MediaItem _mediaItem;
        public MediaItem MediaItem
        {
            get { return _mediaItem; }
            set { _mediaItem = value; }
        }

        private Result _renameResult;
        public Result RenameResult
        {
            get { return _renameResult; }
            set { _renameResult = value; }
        }

        private string _renameResultMessage;
        public string RenameResultMessage
        {
            get { return _renameResultMessage; }
            set { _renameResultMessage = value; }
        }

        public RenameFile(MediaItem mItem)
        {
            this._filePath = Path.GetDirectoryName(mItem.FullName);
            this._originalName = Path.GetFileName(mItem.FullName);
            this.MediaItem = mItem;
        }

        public RenameFile MediaItemRenameFile;

        private System.Windows.Visibility _extraFileVisibility;
        public System.Windows.Visibility ExtraFileVisibility
        {
            get { return _extraFileVisibility; }
            set { _extraFileVisibility = value; }
        }

        public RenameFile(MediaItem mItem, string filePath)
        {
            this._filePath = Path.GetDirectoryName(filePath);
            this._originalName = Path.GetFileName(filePath);
            this.MediaItem = mItem;
        }

        public RenameFile(string filePath)
        {
            this._filePath = Path.GetDirectoryName(filePath);
            this._originalName = Path.GetFileName(filePath);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public void TryRename()
        {
            if (this.NewName != this.OriginalName)
            {
                //validate:
                List<char> illegals = new List<char>();
                foreach (char illegal in Path.GetInvalidFileNameChars())
                {
                    if (this._newName.IndexOf(illegal) > 0)
                    {
                        illegals.Add(illegal);
                    }
                }
                if (illegals.Count == 0)
                {
                    try
                    {
                        string newFullName = Path.Combine(this._filePath, this.NewName);
                        int cnt = 0;

                        while (File.Exists(newFullName))
                        {
                            cnt++;
                            newFullName = Path.Combine(this._filePath,
                                Path.GetFileNameWithoutExtension(this.NewName)
                                + "_" + cnt
                                + Path.GetExtension(this.NewName));
                        }

                        File.Move(Path.Combine(this._filePath, this.OriginalName), newFullName);

                        if (this.ExtraFileVisibility == System.Windows.Visibility.Visible)
                        {
                            MediaBrowserContext.RenameMediaItem(this.MediaItem, Path.GetFileName(newFullName));                        
                        }

                        this.RenameResultMessage = "Umbenannt";
                        this.Rename = false;
                        this.RenameResult = Result.OK;
                    }
                    catch (Exception ex)
                    {
                        this.RenameResultMessage = string.Format("{0}", ex.Message);
                        this.RenameResult = Result.FAIL;
                    }
                }
                else
                {
                    StringBuilder resultBuilder = new StringBuilder();
                    resultBuilder.Append("Nicht umbenannt wegen ungültiger Zeichenfolge ");
                    foreach (char illegal in illegals)
                    {
                        resultBuilder.Append(illegal);
                        resultBuilder.Append(", ");
                    }
                    this.RenameResultMessage = resultBuilder.ToString().TrimEnd(", ".ToCharArray());
                    this.RenameResult = Result.ILLEGAL_CHARACTERS;
                }
            }
            else
            {
                this.RenameResultMessage = "Name unverändert";
                this.RenameResult = Result.OK;
            }
        }
    }
}
