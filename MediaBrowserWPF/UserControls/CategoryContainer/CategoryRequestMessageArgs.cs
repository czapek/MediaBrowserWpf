using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public class CategoryRequestMessageArgs : EventArgs
    {
        public List<Category> SelectedCategories
        {
            get;
            private set;
        }

        public CategoryRequestMessageArgs(Category folder)
        {
            this.SelectedCategories = new List<Category>();
            this.SelectedCategories.Add(folder);
        }
    }
}
