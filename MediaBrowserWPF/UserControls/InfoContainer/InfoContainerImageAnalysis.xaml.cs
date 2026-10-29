using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MediaBrowser4;
using MediaBrowser4.DB.Api;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public partial class InfoContainerImageAnalysis : UserControl
    {
        public InfoContainerImageAnalysis()
        {
            InitializeComponent();
        }

        public void SetInfo(List<MediaItem> mediaItemList)
        {
            this.Clear();

            if (!MediaBrowserContext.IsApiMode)
                return;

            if (mediaItemList == null || mediaItemList.Count == 0)
                return;

            if (mediaItemList.Count > 1)
            {
                this.AddOneLine("Image Analysis", "Nur bei Einzelauswahl verfügbar");
                return;
            }

            try
            {
                ApiImageAnalysisDto analysis = MediaBrowserContext.GetImageAnalysis(mediaItemList[0]);

                if (analysis == null)
                {
                    this.AddOneLine("Image Analysis", "Keine Analyse vorhanden");
                    return;
                }

                this.Build(analysis);
            }
            catch (Exception ex)
            {
                this.AddOneLine("Image Analysis", "Analyse konnte nicht geladen werden: " + ex.Message);
            }
        }

        private void Build(ApiImageAnalysisDto analysis)
        {
            this.AddOneLine("Modell", this.ValueOrDash(analysis.Model));
            this.AddOneLine("Analysiert am", this.FormatDateTime(analysis.CreatedAt ?? analysis.CreationDate));
            this.AddOneLine("Personen", analysis.PersonCount.ToString());
            this.AddOneLine("Tiere", analysis.AnimalCount.ToString());
            this.AddOneLine("Objekte", analysis.ObjectCount.ToString());
            this.AddOneLine("Temperatur", this.FormatTemperature(analysis));
            this.AddOneLine("Wetter", this.ValueOrDash(analysis.WeatherDescription));
            this.AddOneLine("Ort", this.ValueOrDash(analysis.LocationDescription));
            this.AddOneLine("Dokumenttyp", this.ValueOrDash(analysis.FormatOfDocumentOrImage));
            this.AddOneLine("Modell-Temperatur", analysis.ModelTemperature.HasValue
                ? analysis.ModelTemperature.Value.ToString("0.##")
                : "---");
            this.AddOneLine("Dauer", analysis.CreatedInMilliseconds.HasValue
                ? analysis.CreatedInMilliseconds.Value.ToString("n0") + " ms"
                : "---");
            this.AddOneLine("Objektnamen", this.JoinObjects(analysis.Objects));
            this.AddOneLine("Lesbarer Text", this.JoinReadableText(analysis.ReadableText));
            this.AddOneLine("Detailanalyse", this.ValueOrDash(analysis.DetailedImageAnalysis));
        }

        private string JoinObjects(List<ApiImageObjectDto> objects)
        {
            if (objects == null)
                return "---";

            string value = String.Join("; ", objects
                .Where(x => x != null && !String.IsNullOrWhiteSpace(x.ObjectName))
                .Select(x => x.ObjectName));

            return this.ValueOrDash(value);
        }

        private string JoinReadableText(List<ApiImageReadableTextDto> readableText)
        {
            if (readableText == null)
                return "---";

            string value = String.Join("; ", readableText
                .Where(x => x != null && !String.IsNullOrWhiteSpace(x.TextLine))
                .Select(x => x.TextLine));

            return this.ValueOrDash(value);
        }

        private string FormatTemperature(ApiImageAnalysisDto analysis)
        {
            if (analysis.TemperaturMinCelsius.HasValue && analysis.TemperaturMaxCelsius.HasValue)
                return analysis.TemperaturMinCelsius.Value + " °C bis " + analysis.TemperaturMaxCelsius.Value + " °C";

            if (analysis.TemperaturMinCelsius.HasValue)
                return "min. " + analysis.TemperaturMinCelsius.Value + " °C";

            if (analysis.TemperaturMaxCelsius.HasValue)
                return "max. " + analysis.TemperaturMaxCelsius.Value + " °C";

            return "---";
        }

        private string FormatDateTime(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("g") : "---";
        }

        private string ValueOrDash(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "---" : value;
        }

        public void Clear()
        {
            this.ListViewImageAnalysis.Items.Clear();
        }

        private void AddOneLine(string key, string value)
        {
            this.ListViewImageAnalysis.Items.Add(new InfoContainerBaseHelper(key, value));
        }

        private void ListViewImageAnalysis_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.RenderSize.Width > 0)
            {
                this.GridViewImageAnalysis.Columns[0].Width = (this.RenderSize.Width / 2) - 14;
                this.GridViewImageAnalysis.Columns[1].Width = (this.RenderSize.Width / 2) - 14;
            }
        }
    }
}
