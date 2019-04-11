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
using System.Windows.Media.Animation;

namespace MediaBrowserWPF.UserControls.ThumbListContainer
{
    /// <summary>
    /// Interaktionslogik für ThumblistElement.xaml
    /// </summary>
    public partial class ThumblistElement : UserControl
    {
        new public event EventHandler<MediaItemCallbackArgs> MouseDown;
        public event EventHandler<MediaItemCallbackArgs> FadeCompleted;

        public ThumblistElement()
        {
            InitializeComponent();
        }

        public MediaItem SelectedItem
        {
            set
            {
                this.MainImage.DataContext = value;
            }

            get
            {
                return this.MainImage.DataContext as MediaItem;
            }
        }

        public void Fade(double from, double to, double by, int durationMs)
        {
            Storyboard storyBoard = (Storyboard)Resources["StoryBoardFade"];
            storyBoard.Stop();
            this.Thumbnail.Opacity = from;

            DoubleAnimation opacity = (DoubleAnimation)storyBoard.Children[0];    

            opacity.By = by;
            opacity.Duration = new Duration(new TimeSpan(0, 0, 0, 0, durationMs));
            opacity.From = from;
            opacity.To = to;
            opacity.AccelerationRatio = 0.1;
            storyBoard.Begin();      
        }

        private void MediaItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.MouseDown != null && e.LeftButton == MouseButtonState.Pressed)
                this.MouseDown.Invoke(this, new MediaItemCallbackArgs(0, 0, this.MainImage.DataContext as MediaItem));
        }  

        private void StoryboardFade_Completed(object sender, EventArgs e)
        {
            if (this.FadeCompleted != null)
                this.FadeCompleted.Invoke(this, new MediaItemCallbackArgs(0, 0, this.MainImage.DataContext as MediaItem));
        }     
    }
}
