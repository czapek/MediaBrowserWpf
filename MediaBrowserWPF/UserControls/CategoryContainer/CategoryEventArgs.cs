using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls.CategoryContainer
{
    public class CategoryEventArgs : EventArgs
    {
        public CategoryEventArgs(Category category)
        {
            this.Category = category;
        }

        public Category Category { get; private set; }
    }
}
