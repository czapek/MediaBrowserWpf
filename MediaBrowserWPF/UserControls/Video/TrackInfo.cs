using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowserWPF.UserControls.Video
{
    public enum TrackType { Ton, Untertitel };
    public class TrackInfo
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }
        public int Id { get; set; }
        public TrackType Type { get; set; }

        public override string ToString()
        { 
            return this.Name.Replace("Disable", "Deaktiviert");
        }
    }
}
