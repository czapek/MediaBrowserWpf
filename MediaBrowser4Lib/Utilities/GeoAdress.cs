using Gpx;
using MediaBrowser4.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace MediaBrowser4.Utilities
{
    public class GeoAdress
    {
        public string StreetAddressFormatted { get; set; }
        public string PoliticalFormatted { get; set; }
        public string PostalCodeFormatted { get; set; }
        public string Country { get; set; }

        public static GeoAdress GetAdressXml(GeoPoint gps)
        {
            GeoAdress geoAdress = new GeoAdress();
            try
            {
                string url = $"http://maps.google.com/maps/api/geocode/xml?latlng={gps.Latitude} {gps.Longitude}&language=de".Replace(",", ".").Replace(" ", ",");
                XmlDocument doc = new XmlDocument();
                doc.Load(url);


                geoAdress.StreetAddressFormatted = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result[type = \"street_address\"]/formatted_address")?.InnerText;
                geoAdress.PoliticalFormatted = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result[type = \"locality\" and type = \"political\"]/formatted_address")?.InnerText;
                geoAdress.PostalCodeFormatted = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result[type = \"postal_code\"]/formatted_address")?.InnerText;
                geoAdress.Country = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result[type = \"country\" and type = \"political\"]/formatted_address")?.InnerText;
                geoAdress.StreetAddressFormatted = geoAdress.StreetAddressFormatted ?? doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result[type = \"route\"]/formatted_address")?.InnerText;

                if (geoAdress.PoliticalFormatted != null)
                    geoAdress.PoliticalFormatted = Regex.Replace(geoAdress.PoliticalFormatted, @"\d", "").Trim();
            }
            catch (System.Net.WebException)
            {
                
            }

            return geoAdress;
        }
    }
}
