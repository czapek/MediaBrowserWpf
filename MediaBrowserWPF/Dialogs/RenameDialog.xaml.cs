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
using System.ComponentModel;
using MediaBrowser4.Objects;
using SmartRename;
using SmartRename.Interfaces;

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für RenameDialog.xaml
    /// </summary>
    public partial class RenameDialog : Window
    {
        public RenameDialog()
        {
            InitializeComponent();
        }

        private Renamer renamer;

        public RenameDialog(List<MediaItem> mList)
        {
            InitializeComponent();
            Init(mList, "%filename%");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.txtTemplate.Focus();
            TextBox edit = (TextBox)txtTemplate.Template.FindName("PART_EditableTextBox", this.txtTemplate);
            edit.SelectAll();


            edit.ContextMenu = txtContextMenu;
        }

        public RenameDialog(List<MediaItem> mList, string templateText)
        {
            InitializeComponent();

            Init(mList, templateText);
        }

        private void Init(List<MediaItem> mList, string templateText)
        {
            gridIllegal.ToolTip = @"Ersetzt ""/\?*<>. mit der angegebenen Zeichenfolge";
            this.renamer = Renamer.Factory(mList);

            if (mList.Count == 1 && templateText != "%mediadate%{yyMMdd-HHmm-ss}")
            {
                this.txtTemplate.Text = System.IO.Path.GetFileNameWithoutExtension(mList[0].Filename);
            }
            else
            {
                this.txtTemplate.Text = templateText;
            }

            foreach (IReplacement replacement in this.renamer.Replacements.Values.OrderBy(x => x.EscapeKey))
            {
                MenuItem item = new MenuItem();
                item.Header = replacement.EscapeKey;
                item.ToolTip = replacement.HelpText;
                item.Click += new RoutedEventHandler(item_Click);
                txtContextMenu.Items.Add(item);

                if (replacement.EscapeKey == "%metadata%" && mList.Count > 0 && mList[0].MetaData.Count > 0)
                {
                    MenuItem itemMetadata = new MenuItem();                
                    itemMetadata.Header = "Metadata Schlüssel";
                    txtContextMenu.Items.Add(itemMetadata);

                    foreach (string key in mList[0].MetaData.Select(x => x.Name.Substring(0, 1).ToUpper()).Distinct().Distinct().OrderBy(x => x))
                    {
                        item = new MenuItem();
                        item.Header = key + " ...";
                        itemMetadata.Items.Add(item);

                        foreach (MetaData metadata in mList[0].MetaData.Where(x => x.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Name))
                        {
                            MenuItem item2 = new MenuItem();
                            item2.Header = metadata.Name + String.Format(" (\"{0}\")", metadata.Value);
                            item2.Click += new RoutedEventHandler(itemMetadata_Click);
                            item.Items.Add(item2);
                        }
                    }
                }
            }

            this.SetNames();
            this.RenameGrid.DataContext = renamer.RenameFiles;
        }

        void item_Click(object sender, RoutedEventArgs e)
        {
            TextBox edit = (TextBox)txtTemplate.Template.FindName("PART_EditableTextBox", this.txtTemplate);
            edit.SelectedText = "";
            int a = edit.CaretIndex;
            txtTemplate.Text = edit.Text.Insert(edit.CaretIndex, ((MenuItem)sender).Header.ToString());
            edit.CaretIndex = a + ((MenuItem)sender).Header.ToString().Length;
        }

        void itemMetadata_Click(object sender, RoutedEventArgs e)
        {
            string header = ((MenuItem)sender).Header.ToString();
            header = header.Substring(0, header.IndexOf(" (\""));

            TextBox edit = (TextBox)txtTemplate.Template.FindName("PART_EditableTextBox", this.txtTemplate);
            string argument = "{" + header + ":0}";
            int a = edit.CaretIndex;
            txtTemplate.Text = txtTemplate.Text.Insert(edit.CaretIndex, argument);
            edit.CaretIndex = a + argument.Length;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.SetNames();
        }

        private void SetNames()
        {
            this.renamer.RenameFormat = this.txtTemplate.Text.Trim();
            this.renamer.ReplaceIllegal = !this.chkIllegal.IsChecked.Value ? null : this.txtIllegal.Text;

            if (!this.renamer.RenameFormat.Contains(".%ext%") && chkbxExtension.IsChecked == true)
                this.renamer.RenameFormat += ".%ext%{true}";

            this.renamer.SetNewNames();
        }

        private void MenuItemRename_Click(object sender, RoutedEventArgs e)
        {
            foreach (RenameFile kv in this.RenameGrid.SelectedItems)
            {
                kv.Rename = true;
            }
        }

        private void MenuItemNotRename_Click(object sender, RoutedEventArgs e)
        {
            foreach (RenameFile kv in this.RenameGrid.SelectedItems)
            {
                kv.Rename = false;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            this.SetNames();
            this.renamer.CommitNewNames();

            StringBuilder sb = new StringBuilder();
            foreach (RenameFile kv in this.RenameGrid.Items)
            {
                if (kv.RenameResult != Result.OK)
                    sb.AppendLine("(" + kv.NewName + ") " + kv.RenameResultMessage);
            }

            Mouse.OverrideCursor = null;

            if (sb.Length > 0)
            {
                MessageBox.Show(MainWindow.MainWindowStatic,sb.ToString());
            }
            else
                this.Close();
        }
    }
}
