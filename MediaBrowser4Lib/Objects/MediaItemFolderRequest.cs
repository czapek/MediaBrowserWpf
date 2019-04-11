using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class MediaItemFolderRequest : MediaItemRequest
    {
        private List<Folder> folders = new List<Folder>();

        public Folder[] Folders
        {
            get
            {
                return this.folders.ToArray();
            }
        }

        public Folder[] FoldersComplete
        {
            get
            {
                if (this.RequestType == MediaItemRequestType.RECURSIVE)
                {
                    List<Folder> foldersComplete = new List<Folder>();

                    foreach (Folder folder in this.folders)
                    {
                        this.AddFoldersRecursiv(folder, ref foldersComplete);
                    }

                    return foldersComplete.ToArray();
                }
                else
                {
                    return this.folders.ToArray();
                }
            }
        }

        private void AddFoldersRecursiv(Folder root, ref List<Folder>  foldersComplete)
        {
            foldersComplete.Add(root);
            foreach (Folder folder in root.Children)
            {
                if (folder.Id >= 0)
                    foldersComplete.Add(folder);

                AddFoldersRecursiv(folder, ref foldersComplete);
            }
        }

        override public string Header
        {
            get
            {
                string header = (this.folders.Count > 0 ? this.folders[0].Header : String.Empty) 
                    + (this.folders.Count > 1 ?
                " ... " + this.folders.Count + "x" : "");

                return this.UserDefinedName == null ? header : this.UserDefinedName;
            }
        }

        override public string Description
        {
            get
            {
                return (this.RequestType == MediaItemRequestType.RECURSIVE ? "Ordner Rekursiv" : "Ordner Einfach")
                    + (MediaBrowserContext.SearchTokenGlobal != null ? " (Global eingeschränkt)" : "")
                    + ":\n" + String.Join(",\n", folders.OrderBy(x => x.FullPath));
            }
        }

        public void AddFolder(Folder folder)
        {
            //if (!String.IsNullOrEmpty(folder.id))
                this.folders.Add(folder);            

            if (this.folders.Count == 0)
                return;

            this.IsValid = this.folders.Count > 0;
        }

        public void RemoveFolder(Folder folder)
        {
            this.folders.Remove(folder);
            this.IsValid = this.folders.Count > 0;
        }

        public override bool Equals(object obj)
        {
            MediaItemFolderRequest other = obj as MediaItemFolderRequest;

            if (other == null)
                return false;

            if (other.UserDefinedId > 0 && this.UserDefinedId > 0)
            {
                return other.UserDefinedId == this.UserDefinedId;
            }
            else if (!(other.UserDefinedId == 0 && this.UserDefinedId == 0))
            {
                return false;
            }

            if (this.folders.Count != other.folders.Count)
                return false;

            foreach (Folder folder in this.folders)
            {
                if (!other.folders.Contains(folder))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hash = 0;

            foreach (Folder folder in this.folders)
            {
                hash += folder.GetHashCode();
            }

            return hash.GetHashCode();
        }

        /// <summary>
        /// Nötig nach dem DeSerialisieren, da da die
        /// Folder-Objekte dann unvollständig sind
        /// </summary>
        public void RefreshFolders()
        {
            Folder[] foldersOld = this.Folders;

            this.folders.Clear();

            foreach (Folder folder in foldersOld)
                this.AddFolder(MediaBrowserContext.FolderTreeSingelton.GetFolderById(folder.Id));
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemRequest)this.MemberwiseClone();
        }
    }
}
