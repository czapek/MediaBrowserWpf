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
    /// Interaktionslogik für InfoContainerExif.xaml
    /// </summary>
    public partial class InfoContainerExif : UserControl
    {
        private string selectedGroupName = "";
        private Dictionary<string, List<string>> metaDataCache;
        public InfoContainerExif()
        {
            InitializeComponent();
        }

        public void SetInfo(List<MediaItem> mediaItemList)
        {
            this.Clear();

            if (mediaItemList != null && mediaItemList.Count > 0)
            {
                this.Build(mediaItemList);
            }
        }

        private void Build(List<MediaItem> mediaItemList)
        {
            metaDataCache = new Dictionary<string, List<string>>();
            List<string> groupNameList = new List<string>();
            groupNameList.Add("");

            List<MetaData> metaDataList = MediaBrowserContext.GetMetaDataFromMediaItems(mediaItemList);

            if (mediaItemList.Count == 1 && mediaItemList[0].MetaData != null)
            {
                metaDataList = mediaItemList[0].MetaData;
            }

            foreach (MediaBrowser4.Objects.MetaData metadata in metaDataList)
            {
                string uniqueName = metadata.GroupName + "~" + metadata.Name;
                if (metaDataCache.ContainsKey(uniqueName))
                {
                    if (!metaDataCache[uniqueName].Contains(metadata.Value))
                    {
                        metaDataCache[uniqueName].Add(metadata.Value);
                    }
                }
                else
                {
                    metaDataCache[uniqueName] = new List<string>() { metadata.Value };
                }

                if (!groupNameList.Contains(metadata.GroupName))
                {
                    groupNameList.Add(metadata.GroupName);
                }
            }

            foreach (string groupName in groupNameList)
                this.ComboBoxGroups.Items.Add(groupName);

            this.selectedGroupName = this.selectedGroupName ?? "";
            this.ComboBoxGroups.SelectedItem = this.selectedGroupName;

            this.AddFiltered(this.selectedGroupName, this.TextBoxFilter.Text);
        }

        private void AddFiltered(string groupFilter, string nameFilter)
        {
            if (this.metaDataCache == null)
                return;

            nameFilter = nameFilter.Trim().ToLower();

            this.ListViewExif.Items.Clear();
            foreach (KeyValuePair<string, List<string>> kv in this.metaDataCache)
            {
                string name = kv.Key.Split('~')[1];

                if ((groupFilter.Length == 0 || kv.Key.StartsWith(groupFilter))
                    && (nameFilter.Length == 0 || name.ToLower().Contains(nameFilter)))
                    this.ListViewExif.Items.Add(new InfoContainerBaseHelper(name, String.Join("; ", kv.Value)));
            }
        }

        public void Clear()
        {
            this.ListViewExif.Items.Clear();
            this.selectedGroupName = this.ComboBoxGroups.SelectedItem as string;
            this.ComboBoxGroups.Items.Clear();
        }

        private void ListViewExif_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.RenderSize.Width > 0)
            {
                this.GridViewExif.Columns[0].Width = (this.RenderSize.Width / 2) - 14;
                this.GridViewExif.Columns[1].Width = (this.RenderSize.Width / 2) - 14;
            }
        }

        private void ComboBoxGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.AddFiltered(this.ComboBoxGroups.SelectedItem as string ?? "", this.TextBoxFilter.Text);
        }

        private void TextBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.AddFiltered(this.ComboBoxGroups.SelectedItem as string ?? "", this.TextBoxFilter.Text);
        }

        private void MenuItemCopyKey_Click(object sender, RoutedEventArgs e)
        {
            if (this.ListViewExif.SelectedItem != null)
                Clipboard.SetText(((InfoContainerBaseHelper)this.ListViewExif.SelectedItem).Key);
        }

        private void MenuItemCopyValue_Click(object sender, RoutedEventArgs e)
        {
            if (this.ListViewExif.SelectedItem != null)
                Clipboard.SetText(((InfoContainerBaseHelper)this.ListViewExif.SelectedItem).Value);
        }

        private void MenuItemSearch_Click(object sender, RoutedEventArgs e)
        {
            if (this.ListViewExif.SelectedItem != null)
            {
                string key = ((InfoContainerBaseHelper)this.ListViewExif.SelectedItem).Key;
                string value = ((InfoContainerBaseHelper)this.ListViewExif.SelectedItem).Value;

                MainWindow.MainWindowStatic.ShowSearchTab();
                MainWindow.MainWindowStatic.SearchContainer.SearchMetadats(key, value);
            }
        }
    }
}
