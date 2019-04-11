using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;
using System.ComponentModel;

namespace MediaBrowserWPF.UserControls
{
    public class InfoContainerCategoryHelper : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Category Category
        {
            get;
            private set;
        }

        public int Count
        {
            get;
            private set;
        }

        public string Name
        {
            get
            {
                return this.Category + (this.Count > 0 ? " x" + this.Count : "");
            }
        }

        public InfoContainerCategoryHelper(Category category, int count)
        {
            this.Category = category;
            this.Count = count;
        }

        public override string ToString()
        {
            return this.Category.ToString();
        }

        public override int GetHashCode()
        {
            return this.Category.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            InfoContainerCategoryHelper helper = obj as InfoContainerCategoryHelper;

            if (helper == null)
                return false;

            return this.Category.Equals((helper).Category);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
