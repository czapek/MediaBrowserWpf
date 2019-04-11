using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser4.Objects
{
    public class GeoPoint : GeoCoordinate
    {
        public GpsFile GpsFile { get; set; }
        public DateTime LocalTime { get; set; }
        public DateTime UtcTime { get; set; }
        public double DistanceMeter { get; set; }
        public double Acceleration { get; set; }

        private GeoPoint predecessor;
        public GeoPoint Predecessor
        {
            get
            {
                return predecessor;
            }

            set
            {
                this.predecessor = value;
                if (predecessor != null)
                {
                    double distSecond = (this.LocalTime - this.predecessor.LocalTime).TotalSeconds;
                    DistanceMeter = this.GetDistanceTo(this.predecessor);
                    Speed = this.DistanceMeter / distSecond;
                    double distSpeed = this.Speed - this.predecessor.Speed;
                    Acceleration = distSpeed / distSecond;
                }
            }
        }

        public override string ToString()
        {
            return $"{LocalTime:G} {Longitude:n2} {Latitude:n2} {Altitude:n2} {DistanceMeter:n2} m {Speed * 3.6:n2} km/h {Acceleration:n2} m/s2";
        }
    }
}
