using System;
using System.Collections.Generic;

namespace MediaBrowser4.Objects
{
    public sealed class GeoCluster
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Count { get; set; }
        public double MinLatitude { get; set; }
        public double MinLongitude { get; set; }
        public double MaxLatitude { get; set; }
        public double MaxLongitude { get; set; }
        public int? MediaItemId { get; set; }
    }

    public sealed class GeoClusterResult
    {
        public GeoClusterResult()
        {
            this.Clusters = new List<GeoCluster>();
        }

        public List<GeoCluster> Clusters { get; set; }
        public int TotalCount { get; set; }
        public bool Truncated { get; set; }
        public int Zoom { get; set; }
        public int CellSize { get; set; }
    }
}
