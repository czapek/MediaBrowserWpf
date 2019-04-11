using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;
using MediaBrowserWPF.UserControls;

namespace MediaBrowserWPF.Viewer
{
    interface IViewer
    {
        event EventHandler<MediaItemArgument> OnMediaItemChanged;
        double Top { get; set; }
        double Left { get; set; }
        double Width { get; set; }
        double Height { get; set; }
        bool Topmost { get; set; } 
        void Show();
        MediaItem VisibleMediaItem { get; set; }
        bool IsLoaded { get; }
        bool ShowDeleted { get; set; }
        bool IsVisible { get; }
    }
}
