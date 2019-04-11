using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public class MediaItemRequestMessageArgs : EventArgs
    {
        public MediaItemRequest Request
        {
            get;
            private set;
        }

        public MediaItemRequestMessageArgs(MediaItemRequest request)
        {
            this.Request = request;
        }
    }
}
