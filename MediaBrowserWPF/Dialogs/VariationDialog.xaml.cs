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
using System.Windows.Shapes;
using MediaBrowser4.Objects;
using MediaBrowser4;
using MediaBrowserWPF.Viewer;

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für VariationDialog.xaml
    /// </summary>
    public partial class VariationDialog : Window
    {
        private Dictionary<MediaItem, List<Variation>> mitemDic;
        private IMediaViewer mediaViewer;
        private bool rename;

        public enum EditorType { NEW, DELETE, RENAME, SETDEFAULT, COMPLETE };
        private EditorType editorType;

        public VariationDialog()
        {
            InitializeComponent();
        }

        public VariationDialog(List<MediaItem> mitemList, EditorType editorType)
        {
            InitializeComponent();

            this.editorType = editorType;
            this.mitemDic = new Dictionary<MediaItem, List<Variation>>();

            switch (editorType)
            {
                case EditorType.NEW:
                    this.Title = "Variante erstellen";
                    this.chkbxSetDefault.Visibility = System.Windows.Visibility.Visible;
                    break;

                case EditorType.COMPLETE:
                    this.Title = "Varianten ergänzen";
                    this.chkbxSetDefault.Visibility = System.Windows.Visibility.Visible;
                    break;

                case EditorType.DELETE:
                    this.Title = "Variante löschen";
                    break;

                case EditorType.RENAME:
                    this.panelRename.Visibility = System.Windows.Visibility.Visible;
                    this.Title = "Variante umbenennen";             
                    break;

                case EditorType.SETDEFAULT:
                    this.Title = "Zur Hauptvariante machen";
                    break;
            }

            foreach (MediaItem mItem in mitemList)
            {
                this.mitemDic.Add(mItem, MediaBrowserContext.GetVariations(mItem));
            }

            this.mitemDic.Values.SelectMany(x => x).Select(x => x.Name).Distinct().ToList().ForEach(item => this.ddlVariationName.Items.Add(item));
        }

        public VariationDialog(IMediaViewer mediaViewer, bool rename)
        {
            this.rename = rename;
            this.mediaViewer = mediaViewer;
            this.Owner = mediaViewer as Window;
            this.Topmost = true;
            InitializeComponent();
            this.ddlVariationName.ToolTip = null;

            List<Variation> variationList = MediaBrowserContext.GetVariations(this.mediaViewer.VisibleMediaItem);

            if (rename)
            {
                this.ddlVariationName.Text = variationList.FirstOrDefault(x => x.Id == this.mediaViewer.VisibleMediaItem.VariationId).Name;
                this.Title = "Variante umbenennen";
            }
            else
            {
                this.Title = "Variante neu erstellen";
            }

            variationList.Select(x => x.Name).Distinct().ToList().ForEach(item => this.ddlVariationName.Items.Add(item));
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(ddlVariationName.Text.Trim()))
                return;

            if (this.mediaViewer != null)
            {
                if (this.rename)
                {
                    List<Variation> variationList = MediaBrowserContext.GetVariations(this.mediaViewer.VisibleMediaItem);
                    Variation variation = variationList.FirstOrDefault(x => x.Id == this.mediaViewer.VisibleMediaItem.VariationId);
                    variation.Name = ddlVariationName.Text.Trim();
                    MediaBrowserContext.EditVariation(new List<Variation>() {variation});
                    this.mediaViewer.RenameVariation(variation);
                }
                else
                {
                    this.mediaViewer.NewVariation(MediaBrowserContext.SetNewVariation(this.mediaViewer.VisibleMediaItem, ddlVariationName.Text.Trim()));
                }

                this.DialogResult = true;
                this.Close();
            }
            else if (this.mitemDic != null)
            {
                this.OkList();
            }

        }

        private void OkList()
        {
            if (this.editorType == EditorType.DELETE)
            {
                foreach (KeyValuePair<MediaItem, List<Variation>> kv in mitemDic)
                {
                    if (null != kv.Value.FirstOrDefault(x => x.Id == kv.Key.VariationIdDefault && x.Name.Equals(ddlVariationName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase)))
                    {
                        Microsoft.Windows.Controls.MessageBox.Show(MainWindow.MainWindowStatic,"Eine der zu löschenden Varianten ist eine Hauptvariante.", "Löschen nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                foreach (KeyValuePair<MediaItem, List<Variation>> kv in mitemDic)
                {
                    MediaBrowserContext.RemoveVariation(kv.Key, ddlVariationName.Text.Trim());
                }

                this.DialogResult = true;
                this.Close();
            }
            else if (this.editorType == EditorType.COMPLETE)
            {
                foreach (KeyValuePair<MediaItem, List<Variation>> kv in mitemDic)
                {
                    Variation variation = kv.Value.FirstOrDefault(x => x.Name.Equals(ddlVariationName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase));

                    if (variation == null)
                        MediaBrowserContext.SetNewVariation(kv.Key, ddlVariationName.Text.Trim(), this.chkbxSetDefault.IsChecked.Value);
                    else if (this.chkbxSetDefault.IsChecked.Value && variation != null && kv.Value.FirstOrDefault(x => x.Id == kv.Key.VariationIdDefault && x.Name.Equals(ddlVariationName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase)) == null)
                        MediaBrowserContext.SetVariationDefault(kv.Key, variation);
                }

                this.DialogResult = true;
                this.Close();
            }
            else if (this.editorType == EditorType.NEW)
            {
                foreach (KeyValuePair<MediaItem, List<Variation>> kv in mitemDic)
                    MediaBrowserContext.SetNewVariation(kv.Key, ddlVariationName.Text.Trim(), this.chkbxSetDefault.IsChecked.Value);

                this.DialogResult = true;
                this.Close();
            }
            else if (this.editorType == EditorType.RENAME)
            {
                if (txtbxNewName.Text.Trim().Length <= 0)
                    return;

                List<Variation> varList = new List<Variation>();
                foreach (KeyValuePair<MediaItem, List<Variation>> kv in mitemDic)
                {
                    foreach (Variation var in kv.Value.Where(x => x.Name.Equals(ddlVariationName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var.Name = txtbxNewName.Text.Trim();
                        varList.Add(var);
                    }                  
                }

                MediaBrowserContext.EditVariation(varList);

                this.DialogResult = true;
                this.Close();
            }
            else if (this.editorType == EditorType.SETDEFAULT)
            {
                foreach (KeyValuePair<MediaItem, List<Variation>> kv in mitemDic)
                {
                    Variation variation = kv.Value.FirstOrDefault(x => x.Name.Equals(ddlVariationName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase));

                    if (variation != null && kv.Value.FirstOrDefault(x => x.Id == kv.Key.VariationIdDefault && x.Name.Equals(ddlVariationName.Text.Trim(), StringComparison.InvariantCultureIgnoreCase)) == null)
                        MediaBrowserContext.SetVariationDefault(kv.Key, variation);
                }

                this.DialogResult = true;
                this.Close();
            }
        }
    }
}
