using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MediaBrowser4.Objects;
using MediaBrowser4;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für InfoContainerCategories.xaml
    /// </summary>
    public partial class InfoContainerCategories : UserControl
    {
        public InfoContainerCategories()
        {
            InitializeComponent();
            MediaBrowserContext.CategoriesChanged += new EventHandler<MediaItemArg>(MediaBrowserContext_CategoriesChanged);
        }
      

        private List<MediaItem> mediaItemList;
        public void SetInfo(List<MediaItem> mediaItemList)
        {
            this.Clear();
            this.mediaItemList = mediaItemList;

            if (mediaItemList != null && mediaItemList.Count > 0)
            {
                this.Build();
            }
        }

        public void Clear()
        {
            if (this.mediaItemList == null)
                return;

            this.ListBoxCategories.Items.Clear();
        }

        private void Build()
        {
            foreach (KeyValuePair<Category, int> kv in MediaBrowserContext
                .GetCategoriesFromMediaItems(mediaItemList).OrderBy(x => x.Key.IsDate).ThenBy(x => x.Key.IsLocation).ThenBy(x => x.Key.Date).ThenBy(x => x.Key.FullPath))
            {
                this.ListBoxCategories.Items.Add(new InfoContainerCategoryHelper(kv.Key, mediaItemList.Count > 1 ? kv.Value : -1));
            }
        }

        void MediaBrowserContext_CategoriesChanged(object sender, MediaItemArg e)
        {
            if (this.IsVisible)
            {
                this.ListBoxCategories.Items.Clear();
                this.Build();
            }
        }
    }
}
