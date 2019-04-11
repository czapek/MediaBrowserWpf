using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;

namespace MediaBrowser4.Objects
{
    public class CategoryCollection : ObservableCollection<Category>
    {
        public static bool SuppressNotification = false;

        public CategoryCollection()
            : base()
        {
        }

        public CategoryCollection(IEnumerable<Category> categoryCollection)
            : base(categoryCollection)
        {
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!SuppressNotification)
                base.OnCollectionChanged(e);
        }
    }
}
