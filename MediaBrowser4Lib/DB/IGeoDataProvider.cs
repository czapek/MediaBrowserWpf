using System.Collections.Generic;
using MediaBrowser4.Objects;

namespace MediaBrowser4.DB
{
    public interface IGeoDataProvider
    {
        GeoClusterResult GetGeoClusters(double minLongitude, double minLatitude,
            double maxLongitude, double maxLatitude, int zoom, int cellSize, int limit,
            bool includeSingles);

        List<MediaItem> GetGeoItems(double minLongitude, double minLatitude,
            double maxLongitude, double maxLatitude, int limit, int offset, GeoItemsSort sort);
    }
}
