using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public enum DescriptionCommand { SET_AND_CREATE_DESCRIPTION, SET_EXISTING_DESCRIPTION, REMOVE_FROM_MEDIAITEMS, DELETE_DESCRIPTION }
    public class Description
    {
        public DescriptionCommand Command;
        public int? Id;
        public string Value;
        public List<MediaItem> MediaItemList;

        public string ShortString
        {
            get
            {
                if (Value == null)
                    return "";

                string result = Value.Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
                return (result.Length > 50 ? result.Substring(0, 46) + " ..." : result);
            }
        }
    }
}
