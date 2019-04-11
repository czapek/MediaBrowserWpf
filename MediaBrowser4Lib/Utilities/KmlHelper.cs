using MediaBrowser4.Objects;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace MediaBrowser4.Utilities
{
    public class KmlHelper
    {
        public static void OpenDay(DateTime dateMin, DateTime dateMax)
        {
            string kml = GetKml(dateMin, dateMax);
            string temp = Path.GetTempFileName() + ".kml";
            File.WriteAllText(temp, kml, Encoding.UTF8);
            System.Diagnostics.Process.Start(temp);
        }

        public static void ExportKml(DateTime dateMin, DateTime dateMax, string file)
        {
            string kml = GetKml(dateMin, dateMax);
            File.WriteAllText(file, kml, Encoding.UTF8);
        }

        public static string GetKml(DateTime dateMin, DateTime dateMax)
        {
            StringBuilder sb = new StringBuilder();
            bool grt24Hours = (dateMax.Date - dateMin.Date).TotalDays > 1.0;
            List<GeoPoint> geoList = MediaBrowserContext.GetGpsList(dateMin, dateMax);

            if (geoList.Count > 0)
            {
                geoList = geoList.OrderBy(x => x.LocalTime).ToList();
                sb.AppendLine($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><kml xmlns=\"http://www.opengis.net/kml/2.2\" xmlns:gx=\"http://www.google.com/kml/ext/2.2\" xmlns:kml=\"http://www.opengis.net/kml/2.2\" xmlns:atom=\"http://www.w3.org/2005/Atom\"><Document><name>{geoList[0].GpsFile.FileTime:yyyy-MM-ddTHH:mm:ss.fffZ}</name>");
                sb.AppendLine("<Placemark>");
                if (grt24Hours)
                    sb.AppendLine($"<name>{dateMin.Date:dd.MM} - {dateMax.Date:dd.MM.yyyy}</name>");
                else
                    sb.AppendLine($"<name>{dateMin.Date:dd.MM.yyyy}</name>");   

                int lastPoint = 0;
                int cnt = 0;
                double fullDistance = 0;
                Dictionary<int, GeoPoint> placeMarkDic = new Dictionary<int, GeoPoint>();
                Dictionary<DateTime, string> placemarkTag = new Dictionary<DateTime, string>();
                for (int i = 0; i < geoList.Count; i++)
                {
                    double distance = 0;
                    int distanceCnt = i;
                    while (distanceCnt > lastPoint)
                    {
                        distance += geoList[distanceCnt].GetDistanceTo(geoList[distanceCnt - 1]);
                        distanceCnt--;
                    }
              
                    //jede 1000 Meter ein Placemark
                    if(i == 0 || distance > 1000)
                    {                       
                        AddPlacemark(placemarkTag, geoList[i], $"{geoList[i].LocalTime:ddd} {geoList[i].LocalTime:HH:mm} Uhr");
                        placeMarkDic.Add(cnt, geoList[i]);
                        lastPoint = i;
                    }

                    fullDistance += i > 0 ? geoList[i].GetDistanceTo(geoList[i - 1]) : 0;

                    System.Diagnostics.Debug.WriteLine(geoList[i]);
                    cnt++;
                }

                TimeSpan duration = geoList[geoList.Count - 1].LocalTime - geoList[0].LocalTime;
                sb.AppendLine($"<description>Gesamtlänge {fullDistance:n0} Meter, Track-Dauer {duration:g}</description>");
                sb.AppendLine("<gx:Track>");
                sb.AppendLine();

                foreach (GeoPoint point in geoList)
                {
                    sb.AppendLine($"<when>{point.LocalTime:yyyy-MM-ddTHH:mm:ss.fffZ}</when>");
                    sb.AppendLine($"<gx:coord>{point.Longitude} {point.Latitude} {point.Altitude}</gx:coord>".Replace(",", "."));
                    sb.AppendLine();
                }

                sb.AppendLine("</gx:Track>");
                sb.AppendLine("</Placemark>");


                KeyValuePair<int, GeoPoint>? lastPlacemark = null;
                double placemarkInterpolate = 1000.0;
                foreach (KeyValuePair<int, GeoPoint> placeMark in placeMarkDic)
                {
                    double meter = lastPlacemark.HasValue ? placeMark.Value.GetDistanceTo(lastPlacemark.Value.Value) : 0;

                    if (lastPlacemark.HasValue && meter > placemarkInterpolate)
                    {
                        GeoPoint aPoint = lastPlacemark.Value.Value;
                        for (int i = lastPlacemark.Value.Key; i < placeMark.Key; i++)
                        {
                            GeoPoint bPoint = geoList[i];

                            double distance = bPoint.GetDistanceTo(aPoint);

                            if (distance > placemarkInterpolate)
                            {
                                //AddPlacemark(placemarkTag, bPoint, $"+{ bPoint.LocalTime:mm} Min.");
                                aPoint = bPoint;
                            }
                        }
                    }

                    lastPlacemark = placeMark;
                }

                GeoPoint last = geoList[geoList.Count - 1];

                //AddPlacemark(placemarkTag, last, $"+{ last.LocalTime:mm} Min.");

                foreach (string ps in placemarkTag.OrderBy(x => x.Key).Select(x => x.Value))
                {
                    sb.AppendLine(ps);
                }

                sb.AppendLine("</Document></kml>");
            }

            return sb.ToString();
        }

        private static void AddPlacemark(Dictionary<DateTime, string> placemarkTag, GeoPoint last, string name)
        {
            placemarkTag.Add(last.LocalTime,
                                "<Placemark>" + Environment.NewLine +
                                "<name>" + name + "</name>" + Environment.NewLine +
                                $"<Point><coordinates>{last.Longitude} {last.Latitude} {last.Altitude}</coordinates></Point>".Replace(",", ".").Replace(" ", ",") + Environment.NewLine +
                                "</Placemark>");
        }

        public static List<GeoPoint> ParseFolder(string folder, List<GpsFile> ignoreList)
        {
            List<GeoPoint> gpsList = new List<GeoPoint>();

            foreach (String file in Directory.GetFiles(folder, "*.kml", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                DateTime fileDate;
                FileInfo fileInfo = new FileInfo(file);
                if (!DateTime.TryParseExact(fileName, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fileDate))
                {
                    fileDate = fileInfo.CreationTime;
                }

                GpsFile testFile = ignoreList.FirstOrDefault(x => x.FileTime == fileDate);

                if (testFile != null)
                {
                    if (testFile.Filesize == fileInfo.Length)
                    {
                        continue;
                    }
                    else
                    {
                        MediaBrowserContext.DeleteGpsFile(testFile.FileTime);
                    }
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(file);
                GeoPoint geopoint = null;

                foreach (XmlNode node3 in doc.DocumentElement.ChildNodes)
                {
                    foreach (XmlNode node2 in node3.ChildNodes)
                    {
                        foreach (XmlNode node1 in node2.ChildNodes)
                        {
                            if (node1.Name == "gx:Track")
                            {
                                foreach (XmlNode node in node1.ChildNodes)
                                {

                                    if (node.Name == "when")
                                    {
                                        geopoint = new GeoPoint();
                                        geopoint.GpsFile = new GpsFile() { Filesize = fileInfo.Length, FileTime = fileDate };
                                        geopoint.LocalTime = DateTime.Parse(node.InnerText);
                                        geopoint.UtcTime = TimeZone.CurrentTimeZone.ToUniversalTime(geopoint.LocalTime);
                                    }
                                    else if (node.Name == "gx:coord")
                                    {
                                        if (geopoint != null)
                                        {
                                            string[] parts = node.InnerText.Split(' ');
                                            if (parts.Length == 3)
                                            {
                                                geopoint.Longitude = Double.Parse(parts[0], CultureInfo.InvariantCulture);
                                                geopoint.Latitude = Double.Parse(parts[1], CultureInfo.InvariantCulture);
                                                geopoint.Altitude = Double.Parse(parts[2], CultureInfo.InvariantCulture);
                                            }

                                            gpsList.Add(geopoint);
                                            geopoint = null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return gpsList;
        }
    }
}
