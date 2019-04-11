using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class Folder : ITreeNode, INotifyPropertyChanged
    {
        private string fullPath;
        internal string name;
        internal string oldName;

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        [NonSerialized]
        private FolderCollection children;

        public string Header
        {
            get { return this.Name; }
        }

        internal Folder parent;
        public Folder Parent
        {
            get
            {
                return this.parent;
            }

            set
            {
                if (this.parent == value && this.Id > 0)
                    return;

                if (this.parent != null)
                {
                    this.parent.Children.Remove(this);
                }
                else
                {
                    if (this.folderTree != null)
                        this.folderTree.Children.Remove(this);
                }

                this.parent = value;

                if (value != null)
                {
                    if (!value.Children.Contains(this))
                        value.Children.Add(this);
                }
                else
                {
                    if (!this.folderTree.Children.Contains(this))
                        this.folderTree.Children.Add(this);
                }

                this.OnPropertyChanged("ToString");
                this.OnPropertyChanged("FullPath");
            }
        }

        public void UpdateItemInfo()
        {
            this.OnPropertyChanged("ItemCount");
            this.OnPropertyChanged("ItemCountRecursive");
        }

        public FolderCollection Children
        {
            get
            {
                if (children == null)
                {
                    children = new FolderCollection();
                }
                return children;
            }
        }

        public int ChildrenCount
        {
            get
            {
                return Children.Count;
            }
        }

        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }

        public int ItemCountRecursive
        {
            get
            {
                int cnt = this.ItemCount;

                ChildrenRecursiveCount(this, ref cnt);

                return cnt;
            }
        }     

        private void ChildrenRecursiveCount(Folder folder, ref int cnt)
        {
            foreach (Folder children in folder.Children)
            {
                cnt += children.ItemCount;

                ChildrenRecursiveCount(children, ref cnt);
            }
        }

        public System.Windows.Media.Brush FolderColor
        {
            get
            {
                return this.Id < 0 ? System.Windows.Media.Brushes.DarkGray : System.Windows.Media.Brushes.Black;
            }
        }

        public static Folder CreateFolder(string directoryName)
        {
            Folder folder = MediaBrowserContext.FolderTreeSingelton.GetFolderByPath(directoryName);

            if (folder != null)
            {
                if(folder.Id <= 0)
                {
                    MediaBrowserContext.SetFolder(folder);
                }
            }
            else
            {
                FolderCollection children = MediaBrowserContext.FolderTreeSingelton.Children;

                foreach (string part in MediaBrowser4.Objects.FolderTree.GetPathParts(directoryName.Trim()))
                {
                    if (!String.IsNullOrWhiteSpace(part))
                    {
                        Folder folderChild = children.FirstOrDefault(x => x.Name.Equals(part.Trim(), StringComparison.InvariantCultureIgnoreCase));

                        if (folderChild == null)
                        {
                            folderChild = new Folder(part, folder, MediaBrowserContext.FolderTreeSingelton);
                            children.Add(folderChild);
                        }

                        children = folderChild.Children;
                        folder = folderChild;
                    }
                }

                MediaBrowserContext.SetFolder(folder);
            }            

            return folder;
        }

        public List<Folder> ChildrenRecursive()
        {
            List<Folder> folderList = new List<Folder>();

            AddRecursiveFolders(folderList, this);

            return folderList;
        }

        private static void AddRecursiveFolders(List<Folder> folderList, Folder root)
        {
            folderList.Add(root);

            foreach (Folder children in root.Children)
            {
                AddRecursiveFolders(folderList, children);
            }
        }

        public string Name
        {
            get { return name; }
            set
            {
                this.oldName = this.FullPath;
                name = value;
                this.fullPath = null;
                OnPropertyChanged("ToString");
                OnPropertyChanged("FullPath");
                OnPropertyChanged("Name");
            }
        }

        [NonSerialized]
        private FolderTree folderTree;
        public FolderTree FolderTree
        {
            get { return this.folderTree; }
            internal set { this.folderTree = value; }
        }

        public int Id
        {
            get;
            set;
        }

        public string FullPath
        {
            get
            {
                if (this.fullPath == null)
                {
                    Folder node = this;
                    this.fullPath = "";

                    while (node != null)
                    {
                        fullPath = node.Name + "\\" + fullPath;
                        node = node.Parent;
                    }

                    fullPath = fullPath.TrimEnd('\\');
                }

                return this.fullPath;
            }

            set
            {
                if (value != null)
                {
                    this.fullPath = value;
                    this.name = value.Substring(value.LastIndexOf('\\') + 1);
                }
                else
                {
                    this.name = null;
                }
            }
        }

        public int ItemCount
        {
            get; set;
        }

        public Folder()
        {

        }

        internal void SetVirtual()
        {
            this.OnPropertyChanged("FolderColor");
        }


        public Folder(string text, Folder parent, FolderTree folderTree)
        {
            this.name = text;
            this.FolderTree = folderTree;
            this.parent = parent;
        }

        public Folder(FolderTree folderTree)
        {
            this.FolderTree = folderTree;
        }

        public Folder(int id, string foldername, int itemCount)
        {
            this.Id = id;
            this.fullPath = foldername;
            this.ItemCount = itemCount;
            if (this.FullPath.StartsWith("\\\\"))
            {
                string a = this.FullPath.Replace("\\\\", "<<");
                this.name = a.Substring(a.LastIndexOf('\\') + 1);
                this.name.Replace("<<", "\\\\");
            }
            else
            {
                this.name = this.FullPath.Substring(this.FullPath.LastIndexOf('\\') + 1);
            }
        }

        public void Remove()
        {
            this.Silbings.Remove(this);
        }

        /// <summary>
        /// Alle Knoten auf diese Ebene
        /// </summary>
        public FolderCollection Silbings
        {
            get
            {
                if (this.Parent != null)
                {
                    return this.Parent.Children;
                }
                else
                {
                    return this.FolderTree.Children;
                }
            }
        }

        public override bool Equals(object obj)
        {
            Folder folder = obj as Folder;
            return folder == null ? false : folder.FullPath == this.FullPath;
        }

        public override int GetHashCode()
        {
            return this.Id;
        }

        public override string ToString()
        {
            return this.FullPath;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
