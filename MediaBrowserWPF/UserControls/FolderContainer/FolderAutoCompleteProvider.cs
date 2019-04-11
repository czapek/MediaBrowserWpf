using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dragonz.actb.provider;
using MediaBrowser4.Objects;

namespace MediaBrowserWPF.UserControls
{
    public class FolderAutoCompleteProvider : IAutoCompleteDataProvider<Folder>
    {
        private IEnumerable<Folder> _source;

        public FolderAutoCompleteProvider(IEnumerable<Folder> source)
        {
            _source = source;
        }

        IEnumerable<Folder> IAutoCompleteDataProvider<Folder>.GetItems(string textPattern)
        {
            foreach (Folder item in _source)
            {
                if (item.Name.IndexOf(textPattern, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    yield return item;
                }
            }
        }
    }
}
