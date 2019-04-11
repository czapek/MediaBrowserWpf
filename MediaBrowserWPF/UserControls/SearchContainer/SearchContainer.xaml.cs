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
using dragonz.actb.provider;
using OpenCVFaceDetector;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using MediaProcessing.FaceDetection;

namespace MediaBrowserWPF.UserControls
{
    /// <summary>
    /// Interaktionslogik für SearchContainer.xaml
    /// </summary>
    public partial class SearchContainer : UserControl
    {
        public event EventHandler<MediaItemRequestMessageArgs> OnRequest;

        private int currentSearchWindowId = 0;

        public bool IsBuild
        {
            get;
            private set;
        }

        public void Clear()
        {
            this.IsBuild = false;
        }

        public SearchContainer()
        {
            InitializeComponent();
            this.Reset();
        }

        public void Build()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            this.Reset();

            List<string> allMetaDataValues = MediaBrowserContext.GetMetadataKeyList();
            this.MetaDataKeyAutoCompleter.ItemsSource = allMetaDataValues;
            this.MetaDataKeyAutoCompleter.AutoCompleteManager.DataProvider = new SimpleStaticDataProvider(allMetaDataValues);
            this.MetaDataKeyAutoCompleter.AutoCompleteManager.AutoAppend = true;
            this.SearchText1.Focus();

            this.IsBuild = true;

            Mouse.OverrideCursor = null;
        }

        public void Set(MediaItemSearchRequest request)
        {
            this.DataContext = request.SearchToken;
            this.currentSearchWindowId = request.WindowIdentifier;
            this.CheckBoxGlobalUse.IsChecked = false;
            MediaBrowserContext.SearchTokenGlobal = null;
        }

        public void SetSearchToken(MediaItemRequest request)
        {
            if (request.SearchTokenCombined == null)
            {
                return;
            }

            if (!this.IsBuild)
            {
                this.Build();
            }

            this.DataContext = request.SearchTokenCombined;
            this.CheckBoxGlobalUse.IsChecked = true;
        }

        public void SearchMetadats(string key, string value)
        {
            if (this.MetaDataKeyAutoCompleter.Items.Contains(key))
            {
                this.MetaDataKeyAutoCompleter.Text = key;
            }

            this.MetaDataValue.Text = value;

            SearchToken searchToken = (SearchToken)this.DataContext;

            searchToken.MetaDataValue = value;
            searchToken.MetaDataKey = key;

            this.StartSearch();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            this.StartSearch();
        }

        private void StartSearch()
        {
            SearchToken searchToken = (SearchToken)this.DataContext;

            if (!searchToken.IsValid)
                return;

            if (this.CheckBoxNewTab.IsChecked.Value)
            {
                this.currentSearchWindowId++;
                this.CheckBoxNewTab.IsChecked = false;
            }

            MediaItemSearchRequest searchRequest = new MediaItemSearchRequest((SearchToken)this.DataContext, this.currentSearchWindowId);

            if (this.OnRequest != null)
            {
                this.OnRequest(this, new MediaItemRequestMessageArgs(searchRequest));
            }
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            this.Reset();
        }

        private void Reset()
        {
            SearchToken searchToken = new SearchToken();
            this.DataContext = searchToken;
            this.CheckBoxGlobalUse.IsChecked = false;
            MediaBrowserContext.SearchTokenGlobal = null;
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource.GetType().Equals(typeof(System.Windows.Controls.Primitives.ToggleButton)))
                this.SearchText1.Focus();

            if (e.Key == Key.Enter)
            {
                if (this.MetaDataKeyAutoCompleter.IsKeyboardFocusWithin)
                    return;

                //den Focus wechseln und wieder herstellen um Databinding an alle Elemente zu sichern
                Control keepFocus = this.focusedElement;
                this.ButtonStart.Focus();
                this.StartSearch();
                if (keepFocus != null)
                {
                    keepFocus.Focus();
                }
            }
        }

        private void CheckBoxGlobalUse_Checked(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.SearchTokenGlobal = (SearchToken)this.DataContext;
        }

        private void CheckBoxGlobalUse_Unchecked(object sender, RoutedEventArgs e)
        {
            MediaBrowserContext.SearchTokenGlobal = (SearchToken)null;
        }

        Control focusedElement;
        private void DockPanel_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            focusedElement = e.Source as Control;
        }
    }
}
