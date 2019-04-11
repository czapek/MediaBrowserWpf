using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowser4.Utilities
{
    public class MediaItemInfo
    {
        public static string SimpleInfo(MediaItem mItem)
        {

            string resultMessage = String.Empty;
            String meteo = String.Empty;
            Category catFotograf = mItem.Categories.FirstOrDefault(x => x.FullPath.IndexOf("fotografen", StringComparison.InvariantCultureIgnoreCase) >= 0);
            if (catFotograf == null || catFotograf.FullPath.IndexOf("czapek", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                var m = MeteoData(mItem);
                meteo = m != null ? String.Format("{0}°C, {1}%", Math.Round(m.Item1), Math.Round(m.Item2)) : String.Empty;
            }

            Category dateCat = mItem.Categories.FirstOrDefault(x => x.IsDate);
            Category locCat = mItem.Categories.FirstOrDefault(x => x.IsLocation);
            if (locCat == null)
            {
                //resultMessage = String.Join(", ", mItem.Categories.Select(x => x.NameDate));

                resultMessage = (dateCat != null ? (dateCat.Date == mItem.MediaDate.Date ? mItem.MediaDate.ToString("d")
                    + " (" + mItem.MediaDate.ToString("HH:mm") + ")" : dateCat.Date.ToString("d"))
                    + (mItem.Categories.Count > 1 ? ", " : String.Empty) : String.Empty)
                    + String.Join(", ", mItem.Categories.Where(x => !x.IsDate).Select(x => x.NameDate));
            }
            else
            {
                resultMessage = (dateCat != null ?
                    (dateCat.Date == mItem.MediaDate.Date ? mItem.MediaDate.ToString("d")
                    + " (" + mItem.MediaDate.ToString("HH:mm") + ")" : dateCat.Date.ToString("d"))
                    + (locCat != null ? ", " : String.Empty) : String.Empty)
                    + (locCat != null ? locCat.Name + (locCat.Parent != null ? " (" + locCat.Parent.Name + ")" : "") : String.Empty);
            }

            return resultMessage + (resultMessage.Length != 0 && meteo.Length != 0 ? ", " : "") + meteo;
        }

        public static Tuple<double, double> MeteoData(MediaItem mItem)
        {
            SortedDictionary<DateTime, Tuple<double, double>> meteorologyData
                = MediaBrowserContext.GetMeteorologyData(mItem.MediaDate.Date,
                mItem.MediaDate.Date.AddDays(1));

            if (meteorologyData.Count > 0)
            {
                DateTime stop = meteorologyData.Keys.FirstOrDefault(x => x > mItem.MediaDate);
                DateTime start = meteorologyData.Keys.LastOrDefault(x => x < mItem.MediaDate);

                start = start == default(DateTime) ? start = DateTime.MinValue : start;
                stop = stop == default(DateTime) ? stop = DateTime.MaxValue : stop;

                double a = (mItem.MediaDate - start).TotalSeconds;
                double b = (stop - mItem.MediaDate).TotalSeconds;

                double min = Math.Min(a, b);

                if (min < 1800)
                {
                    return meteorologyData[a == min ? start : stop];
                }
            }
            return null;
        }
    }
}
