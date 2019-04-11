using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaBrowserWPF.UserControls
{
    public class InfoContainerBaseHelper : INotifyPropertyChanged
    {
        public const int MaxCharacterLength = 100;
        public event PropertyChangedEventHandler PropertyChanged;

        public string Key
        {
            get;
            private set;
        }

        public string Value
        {
            get;
            private set;
        }

        public string Name
        {
            get
            {
                return this.Value == null ? String.Empty : this.Value.Length < MaxCharacterLength ? this.Value : this.Value.Substring(0, MaxCharacterLength - 4) + " ...";
            }
        }

        public string ToolTip
        {
            get
            {
                return this.Value;
            }
        }

        public InfoContainerBaseHelper(string key, string value)
        {
            this.Key = key;         
            this.Value = value;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
