namespace ApiMock
{
    public sealed class GeoClusterQueryInput
    {
        public double? MinLongitude { get; set; }
        public double? MinLatitude { get; set; }
        public double? MaxLongitude { get; set; }
        public double? MaxLatitude { get; set; }
        public int? Zoom { get; set; }
        public int? CellSize { get; set; }
        public int? Limit { get; set; }
        public bool? IncludeSingles { get; set; }
    }

    public sealed class GeoItemsQueryInput
    {
        public double? MinLongitude { get; set; }
        public double? MinLatitude { get; set; }
        public double? MaxLongitude { get; set; }
        public double? MaxLatitude { get; set; }
        public int? Limit { get; set; }
        public int? Offset { get; set; }
        public string Sort { get; set; }
    }
}
