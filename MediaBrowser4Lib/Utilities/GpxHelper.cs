using Gpx;
using MediaBrowser4.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser4.Utilities
{
    public static class GpxHelper
    {
        public static void ExportGpx(DateTime dateMin, DateTime dateMax, string file)
        {
            List<GeoPoint> geoList = MediaBrowserContext.GetGpsList(dateMin, dateMax);

            if (geoList.Count > 0)
            {
                GpxTrack track = new GpxTrack();
                GpxTrackSegment segment = new GpxTrackSegment();

                foreach (GeoPoint geo in geoList)
                {
                    GpxTrackPoint point = new GpxTrackPoint()
                    {
                        Longitude = geo.Longitude,
                        Latitude = geo.Latitude,
                        Elevation = geo.Altitude,
                        Time = geo.UtcTime
                    };

                    System.Diagnostics.Debug.WriteLine(geo);
                    segment.TrackPoints.Add(point);
                }

                track.Segments.Add(segment);

                if (!Directory.Exists(Path.GetDirectoryName(file)))
                    Directory.CreateDirectory(Path.GetDirectoryName(file));

                using (FileStream fileStream = File.Create(file))
                {
                    using (GpxWriter writer = new GpxWriter(fileStream))
                    {
                        writer.WriteTrack(track);
                    }
                }

                System.Diagnostics.Process.Start(file);
            }
        }
    }
}
