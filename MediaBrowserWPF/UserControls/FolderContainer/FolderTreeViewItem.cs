using System.Windows.Controls;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public class FolderTreeViewItem : TreeViewItem
    {
        public Folder Folder
        {
            get;
            private set;
        }

        public FolderTreeViewItem(string directoryName)
        {
            this.Header = directoryName;
        }

        public FolderTreeViewItem(Folder folder)
        {
            this.Header = System.IO.Path.GetFileName(folder.FullPath);
            this.Folder = folder;
        }
    }
}
