using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using MediaBrowser4.DB;
using System.Collections.ObjectModel;
using MediaBrowser4.Utilities;

namespace MediaBrowser4.Objects
{
    public class CategoryTree
    {
        public int ChangeVersion;
        private CategoryCollection children = new CategoryCollection();
        public CategoryCollection FullCategoryCollection { get; private set; }

        public CategoryCollection Children
        {
            get
            {
                return this.children;
            }
        }

        public CategoryTree(CategoryCollection children, Dictionary<int, Category> categoryDictionary)
        {
            this.FullCategoryCollection = new CategoryCollection(categoryDictionary.Values.OrderBy(x => x.IsDate).ThenBy(x => x.IsLocation).ThenBy(x => x.Date).ThenBy(x => x.FullPath));

            this.children = children;

            foreach (Category cat in categoryDictionary.Values)
            {
                cat.CategoryTree = this;
            }
        }

        public void Add(Category category)
        {
            if (this.FullCategoryCollection.FirstOrDefault(x => x.Id == category.Id) == null && category.Id > 0)
            {
                category.CategoryTree = this;
                this.FullCategoryCollection.Add(category);
            }
        }

        public void Remove(Category category)
        {
            if (category.Parent == null)
            {
                this.children.Remove(category);
            }
            else
            {
                category.Parent.Children.Remove(category);
            }

            this.FullCategoryCollection.Remove(category);
        }

        public Category GetcategoryById(int id)
        {
            return this.FullCategoryCollection.FirstOrDefault(x => x.Id == id);
        }

        public List<Category> CategorizeByExifDate(List<MediaItem> mList)
        {
            return CategorizeByExifDate(mList, null);
        }

        public List<Category> CategorizeByExifDate(List<MediaItem> mList, DateTime? date)
        {
            List<Category> categoryList = new List<Category>();

            try
            {
                string diaryCategorizeFolder = date == null ? MediaBrowserContext.GetDBProperty("DiaryCategorizeFolder") : MediaBrowserContext.CategoryHistoryName;

                if (diaryCategorizeFolder == null
                    || diaryCategorizeFolder.Trim().Length == 0)
                    return categoryList;

                string path, sortPath;
                Dictionary<string, MediaBrowser4.Objects.Category> catPath = new Dictionary<string, MediaBrowser4.Objects.Category>();

                Dictionary<MediaBrowser4.Objects.Category, List<MediaBrowser4.Objects.MediaItem>> mediaItemCat
                            = new Dictionary<MediaBrowser4.Objects.Category, List<MediaBrowser4.Objects.MediaItem>>();


                foreach (MediaItem mItem in mList)
                {
                    DateTime newDate = date == null ? mItem.MediaDate : date.Value;

                    if (DateTime.MinValue == newDate)
                        continue;

                    path = diaryCategorizeFolder + "\\"
                        + newDate.ToString("yyyy") + "\\"
                        + newDate.ToString("MMMM") + "\\"
                        + newDate.ToString("d. ").PadLeft(4, ' ')
                        + newDate.ToString("dddd");

                    sortPath = diaryCategorizeFolder + "\\"
                        + newDate.ToString("yyyy") + "\\"
                        + newDate.ToString("MM") + "\\"
                        + newDate.ToString("dd");

                    if (!catPath.ContainsKey(path))
                    {
                        Category category = MediaBrowserContext.SetCategoryByPath(path, sortPath);
                        catPath.Add(path, category);

                        List<MediaItem> subList = new List<MediaItem>();
                        subList.Add(mItem);
                        mediaItemCat.Add(category, subList);
                    }
                    else
                    {
                        mediaItemCat[catPath[path]].Add(mItem);
                    }
                }

                foreach (KeyValuePair<MediaBrowser4.Objects.Category, List<MediaBrowser4.Objects.MediaItem>> kv in mediaItemCat)
                {
                    categoryList.Add(kv.Key);
                    List<MediaBrowser4.Objects.Category> catList = new List<MediaBrowser4.Objects.Category>() { kv.Key };
                    MediaBrowserContext.CategorizeMediaItems(kv.Value, catList);
                }
            }
            catch(Exception)
            {
  
            }

            return categoryList;
        }
    }
}
