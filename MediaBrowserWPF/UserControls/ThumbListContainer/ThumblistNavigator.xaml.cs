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

namespace MediaBrowserWPF.UserControls.ThumbListContainer
{
    /// <summary>
    /// Interaktionslogik für ThumblistNavigator.xaml
    /// </summary>
    public partial class ThumblistNavigator : UserControl
    {
        new public event EventHandler<MediaItemCallbackArgs> MouseDown;

        public List<MediaItem> MediaItemList
        {
            set;
            get;
        }

        private MediaItem selectedItem;
        public MediaItem SelectedItem
        {
            set
            {
                if (this.Visibility != System.Windows.Visibility.Visible)
                    return;

                this.selectedItem = value;

                //ThumblistElement element = this.Find(value);

                //if (element == null)
                //{
                    this.ShowList();
                //}
                //else
                //{
                //    element.FadeCompleted += new EventHandler<MediaItemCallbackArgs>(element_FadeCompleted);
                //    element.Fade(1, 0, 1, 500);
                //}
            }

            get
            {
                return this.selectedItem;
            }
        }

        void element_FadeCompleted(object sender, MediaItemCallbackArgs e)
        {
            this.ShowList();
        }

        public ThumblistNavigator()
        {
            InitializeComponent();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.ShowList();
        }

        private ThumblistElement Find(MediaItem mediaItem)
        {
            foreach (ThumblistElement element in this.MainPanelLeft.Children)
            {
                if (element.SelectedItem == mediaItem)
                {
                    return element;
                }
            }

            foreach (ThumblistElement element in this.MainPanelRight.Children)
            {
                if (element.SelectedItem == mediaItem)
                {
                    return element;
                }
            }

            return null;
        }

        private void ShowList()
        {
            if (this.Visibility != System.Windows.Visibility.Visible)
                return;

            this.MainPanelLeft.Children.Clear();
            this.MainPanelRight.Children.Clear();

            if (this.MediaItemList == null || this.MediaItemList.Count == 0)
                return;

            if (this.SelectedItem == null)
                this.SelectedItem = this.MediaItemList[0];


            int cntLeft = (int)(this.MainPanelLeft.ActualWidth / 156.0);
            int cntRight = (int)(this.MainPanelRight.ActualWidth / 156.0);
            int pos = this.MediaItemList.IndexOf(this.SelectedItem);

            int start = Math.Max(0, pos - cntLeft);
            int stop = Math.Min(this.MediaItemList.Count - 1, pos + cntRight + 1);

            int delay = 0;
            for (int i = pos - 1; i >= start; i--)
            {
                delay++;
                ThumblistElement imageControl = new ThumblistElement();
                imageControl.Fade(0, 1, 1, 500 * delay);
                imageControl.SelectedItem = this.MediaItemList[i];
                imageControl.MouseDown += new EventHandler<MediaItemCallbackArgs>(imageControl_MouseDown);
                this.MainPanelLeft.Children.Add(imageControl);
            }

            delay = 0;
            for (int i = pos + 1; i < stop; i++)
            {
                delay++;
                ThumblistElement imageControl = new ThumblistElement();
                imageControl.Fade(0, 1, 1, 500 * delay);
                imageControl.SelectedItem = this.MediaItemList[i];
                imageControl.MouseDown += new EventHandler<MediaItemCallbackArgs>(imageControl_MouseDown);
                this.MainPanelRight.Children.Add(imageControl);
            }

            this.MainPanelCenter.ToolTip = this.MediaItemList[pos].ToolTip;
        }

        void imageControl_MouseDown(object sender, MediaItemCallbackArgs e)
        {
            if (this.MouseDown != null)
                this.MouseDown.Invoke(this, e);
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Visibility = System.Windows.Visibility.Hidden;
        }
    }
}
