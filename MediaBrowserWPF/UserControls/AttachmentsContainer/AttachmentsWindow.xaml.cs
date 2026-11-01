using System.Collections.Generic;
using System.Windows;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public partial class AttachmentsWindow : Window
    {
        public AttachmentsWindow()
        {
            InitializeComponent();
        }

        public AttachmentsWindow(List<MediaItem> mediaItems, Window owner)
            : this()
        {
            if (owner != null)
                this.Owner = owner;

            this.Title = "Anhänge" + (mediaItems == null || mediaItems.Count == 0
                ? string.Empty
                : " (" + mediaItems.Count + " Medien)");

            this.attachmentsContainer.SetInfo(mediaItems);
        }
    }
}
