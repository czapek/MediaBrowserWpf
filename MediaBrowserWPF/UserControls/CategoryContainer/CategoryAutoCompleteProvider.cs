using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dragonz.actb.provider;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public class CategoryAutoCompleteProvider : IAutoCompleteDataProvider<Category>
    {
        private IEnumerable<Category> _source;

        public CategoryAutoCompleteProvider(IEnumerable<Category> source)
        {
            _source = source;
        }  

        IEnumerable<Category> IAutoCompleteDataProvider<Category>.GetItems(string textPattern)
        {
            foreach (Category item in _source)
            {
                if (item.Name.IndexOf(textPattern, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    yield return item;
                }
            }
        }
    }
}
