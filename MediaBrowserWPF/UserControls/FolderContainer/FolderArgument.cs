using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public class FolderArgument : EventArgs
    {
        public Folder Folder
        {
            get;
            private set;
        }

        public FolderArgument(Folder folder)
        {
            this.Folder = folder;
        }        
    }
}
