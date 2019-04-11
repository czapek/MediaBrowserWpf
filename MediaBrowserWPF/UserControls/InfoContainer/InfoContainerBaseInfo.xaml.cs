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
    /// Interaktionslogik für InfoContainerBaseInfo.xaml
    /// </summary>
    public partial class InfoContainerBaseInfo : UserControl
    {
        public InfoContainerBaseInfo()
        {
            InitializeComponent();
        }

        public void SetInfo(List<MediaItem> mediaItemList)
        {
            this.Clear();

            if (mediaItemList != null && mediaItemList.Count > 0)
            {
                if (mediaItemList.Count == 1)
                    this.Build(mediaItemList[0]);
                else
                    this.Build(mediaItemList);
            }
        }

        private void Build(List<MediaItem> mediaItemList)
        {
            DateTime maxdate = mediaItemList.Select(x => x.MediaDate).Max<DateTime>();
            DateTime mindate = mediaItemList.Select(x => x.MediaDate).Min<DateTime>();

            AddOneLine("Dateien", mediaItemList.Count + " ausgewählt");
            AddOneLine("Dateien-Größe", String.Format("{0:0,0}", mediaItemList.Select(x => x.FileLength).Min<long>() / 1024)
                + " - " + String.Format("{0:0,0}", mediaItemList.Select(x => x.FileLength).Max<long>() / 1024) + " KByte");
            AddOneLine("Dateien-Größe (Summe)", String.Format("{0:0,0}", mediaItemList.Select(x => x.FileLength).Sum() / 1024) + " KByte");
            AddOneLine("Erstell-Datum (EXIF)", mediaItemList.Select(x => x.MediaDate).Min<DateTime>().ToString("d")
                + " - " + mediaItemList.Select(x => x.MediaDate).Max<DateTime>().ToString("d"));
            AddOneLine("Priorität", mediaItemList.Select(x => x.Priority).Min<int>()
                + " - " + mediaItemList.Select(x => x.Priority).Max<int>());

            List<string> pathList = mediaItemList.Select(x => System.IO.Path.GetDirectoryName(x.FullName)).Distinct().ToList();
            pathList.Sort();

            foreach (var path in pathList)
            {
                AddOneLine("Datei-Pfad", path);
            }

            MediaBrowserContext.GetDescription(mediaItemList);
            List<string> descList = mediaItemList.Where(x => x.Description != null && x.Description.Length > 0)
                .Select(x => (x.Description.Length < InfoContainerBaseHelper.MaxCharacterLength ? x.Description : x.Description.Substring(0, InfoContainerBaseHelper.MaxCharacterLength - 4) + " ...")).Distinct().ToList();
            descList.Sort();

            foreach (var desc in descList)
            {
                if (desc != null && desc.Length != 0)
                {
                    AddOneLine("Beschreibung", desc);
                }
            }
        }       

        private void Build(MediaItem mItem)
        {
            AddOneLine("Datei-Name", mItem.Filename);
            AddOneLine("Datei-Pfad", mItem.Foldername);

            AddOneLine("Datei-Größe", String.Format("{0:0,0}", mItem.FileLength) + " Byte");                       
            AddOneLine("Bild-Größe", mItem.WidthOrientation + " x " + mItem.HeightOrientation + " = " + String.Format("{0:n1}", ((double)mItem.Width * (double)mItem.Height) / 1000000) + " Mio Pixel");

            System.Drawing.Size? croppedSize = mItem.CroppedSize;
            if (croppedSize != null)
            {
                AddOneLine("Beschnitten", croppedSize.Value.Width + " x " + croppedSize.Value.Height + " = " + String.Format("{0:n1}", ((double)croppedSize.Value.Width * (double)croppedSize.Value.Height) / 1000000) + " Mio Pixel");
            }            
            
            AddOneLine("Seitenverhältnis", String.Format("{0} ({1:0.00})", mItem.AspectRatioString, (double)Math.Max(mItem.Width, mItem.Height) / (double)Math.Min(mItem.Width, mItem.Height)));

            if (croppedSize != null)
            {
                AddOneLine("Beschnitten", String.Format("{0} ({1:0.00})", mItem.AspectRatioStringCropped, (double)Math.Max(croppedSize.Value.Width, croppedSize.Value.Height) / (double)Math.Min(croppedSize.Value.Width, croppedSize.Value.Height)));
            } 
            
            AddOneLine("Erstell-Datum (EXIF)", mItem.MediaDate.ToString());
            AddOneLine("Erstell-Datum (Datei)", mItem.CreationDate.ToString());
            AddOneLine("Einfüge-Datum (DB)", mItem.InsertDate.ToString());

            if (mItem.FileObject.Exists)
            {
                AddOneLine("geändert (lokal)", mItem.FileObject.LastWriteTime.ToString());
                AddOneLine("letzter Zugriff (lokal)", mItem.FileObject.LastAccessTime.ToString());
            }

            AddOneLine("Priorität", mItem.Priority.ToString());
            AddOneLine("MD5 Wert", mItem.Md5Value);

            MediaBrowserContext.GetDescription(mItem);
            AddOneLine("Beschreibung", mItem.Description == null || mItem.Description.Length == 0 ? " ---" : mItem.Description);

            foreach (string text in MediaBrowser4.Objects.ExtraData.GetLocalStickyFiles(mItem, null))
            {
                AddOneLine("Extrafile", System.IO.Path.GetFileName(text));
            }

            foreach (KeyValuePair<int, string> kv in MediaBrowserContext.GetDublicates(mItem, MediaItem.DublicateCriteria.CHECKSUM))
            {
                AddOneLine("Dublikate", kv.Value);
            }

            if (mItem is MediaBrowser4.Objects.MediaItemVideo)
            {
                AddOneLine("Abspieldauer", (new TimeSpan((long)(mItem.Duration * 10000000))).ToString().Substring(0, 8));
                AddOneLine("Einzelbilder", mItem.Frames.ToString() + " total");
                AddOneLine("Bilder/Sekunde", String.Format("{0:0.0}", mItem.Fps) + " per second");

                MediaBrowser4.Objects.MetaData aspect = mItem.MetaData.FindSoft("aspect ratio");
                if (!aspect.Null)
                    AddOneLine("Seitenverhältnis", aspect.Value);
            }
        }

        public void Clear()
        {
            this.ListViewBase.Items.Clear();
        }

        private void AddOneLine(string key, string value)
        {
            this.ListViewBase.Items.Add(new InfoContainerBaseHelper(key, value));
        }

        private void ListViewBase_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.RenderSize.Width > 0)
            {
                this.GridViewBase.Columns[0].Width = (this.RenderSize.Width / 2) - 14;
                this.GridViewBase.Columns[1].Width = (this.RenderSize.Width / 2) - 14;
            }
        }
    }
}
