using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public enum MediaItemFilesRequestType { FilesNotExist, FilesFromList, FilesFromChecksum }

    public class MediaItemFilesRequest : MediaItemRequest
    {
        public MediaItemFilesRequestType FilesRequestType { get; private set; }

        public MediaItemFilesRequest(MediaItemFilesRequestType requestType)
        {
            this.FilesRequestType = requestType;
            this.IsValid = true;
        }

        public List<string> FileList
        {
            get;
            private set;
        }

        private string _fileListName;
        public string FileListName
        {
            set
            {
                _fileListName = value;
                if (File.Exists(_fileListName))
                    this.FileList = File.ReadAllLines(_fileListName).ToList();
                else
                    this.FileList = new List<string>();
            }

            get
            {
                return _fileListName;
            }
        }


        public override string Header
        {
            get
            {
                switch (this.FilesRequestType)
                {
                    case MediaItemFilesRequestType.FilesNotExist:
                        return "Fehlende Dateien";

                    case MediaItemFilesRequestType.FilesFromList:
                        return "Dateien aus Liste";

                    case MediaItemFilesRequestType.FilesFromChecksum:
                        return "Dateien aus Liste";

                    default:
                        throw new Exception("Unknown RequestType: " + this.FilesRequestType);
                }
            }
        }

        public override string Description
        {
            get
            {
                switch (this.FilesRequestType)
                {
                    case MediaItemFilesRequestType.FilesNotExist:
                        return "Zeigt nur Medien für die keine passende Datei gefunden werden kann.";

                    case MediaItemFilesRequestType.FilesFromList:
                        return this.FileListName;

                    case MediaItemFilesRequestType.FilesFromChecksum:
                        return this.FileListName;

                    default:
                        throw new Exception("Unknown RequestType: " + this.FilesRequestType);
                }
            }
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            return this.GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode()
        {
            return (int)this.FilesRequestType;
        }
    }
}
