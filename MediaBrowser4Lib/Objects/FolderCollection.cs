using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace MediaBrowser4.Objects
{
    public class FolderCollection : ObservableCollection<Folder>
    {
        public FolderCollection()
            : base()
        {
        }

        public FolderCollection(IEnumerable<Folder> folderCollection)
            : base(folderCollection)
        {
        }       
    }
}
