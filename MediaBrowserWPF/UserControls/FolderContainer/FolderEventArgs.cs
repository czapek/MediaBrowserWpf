using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls.FolderContainer
{
    public class FolderEventArgs : EventArgs
    {
        public FolderEventArgs(Folder folder)
        {
            this.Folder = folder;
        }

        public Folder Folder { get; private set; }
    }
}
