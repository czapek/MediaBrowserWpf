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

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für InfoContainer.xaml
    /// </summary>
    public partial class InfoContainer : UserControl
    {
        private double defaultExpanderColapsedHeight;
        List<Expander> expanderList;
        public InfoContainer()
        {
            InitializeComponent();
        }

        public void Clear()
        {
            this.infoCategories.Clear();
            this.infoExif.Clear();
            this.infoBase.Clear();
        }

        public void SetInfo(List<MediaItem> mediaItemList)
        {
            if (mediaItemList != null && mediaItemList.Count > 0)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                this.Build(mediaItemList);
                Mouse.OverrideCursor = null;
            }
        }

        private void Build(List<MediaItem> mediaItemList)
        {
            this.infoCategories.SetInfo(mediaItemList);
            this.infoBase.SetInfo(mediaItemList);
            this.infoExif.SetInfo(mediaItemList);
        }

        private void SetExpanderSize()
        {
            if ( this.ExifExpander == null
                || this.CategoryExpander == null
                || this.BaseInfoExpander == null)
                return;

            if (this.defaultExpanderColapsedHeight == 0 && this.CategoryExpander.RenderSize.Height > 0)
            {
                this.defaultExpanderColapsedHeight = this.CategoryExpander.RenderSize.Height;

                this.expanderList = new List<Expander>();
                this.expanderList.Add(this.CategoryExpander);
                this.expanderList.Add(this.BaseInfoExpander);
                this.expanderList.Add(this.ExifExpander);
            }

            if (this.expanderList != null)
            {
                int openExpanders = this.expanderList.Where(x => x.IsExpanded).Count();
                double availableHeight
                    = ((this.RenderSize.Height - (this.expanderList.Count * this.defaultExpanderColapsedHeight)) / openExpanders) + this.defaultExpanderColapsedHeight;

                if (availableHeight >= this.defaultExpanderColapsedHeight && availableHeight < 50000)
                {
                    foreach (Expander expander in this.expanderList)
                    {
                        expander.Height = expander.IsExpanded ? availableHeight : this.defaultExpanderColapsedHeight;
                    }
                }
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.SetExpanderSize();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            this.SetExpanderSize();
        }      
    }
}
