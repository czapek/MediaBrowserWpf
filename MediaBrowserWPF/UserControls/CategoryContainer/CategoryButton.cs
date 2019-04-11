using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls.CategoryContainer
{
    public class CategoryButton : Button
    {
        public Category Category { get; set; }
        public List<Category> CategoryList { get; set; }
    }
}
