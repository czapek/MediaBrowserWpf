using System.Windows;

namespace MediaBrowserWPF.UserControls
{
    public partial class GeoMapWindow : Window
    {
        public GeoMapWindow(Window owner)
        {
            InitializeComponent();

            if (owner != null)
                this.Owner = owner;

            this.geoDataContainer.SetLargeView();
            this.geoDataContainer.OnRequest += (sender, args) =>
            {
                if (this.Owner is MainWindow)
                    ((MainWindow)this.Owner).HandleGeoDataRequest(args);
            };
        }
    }
}
