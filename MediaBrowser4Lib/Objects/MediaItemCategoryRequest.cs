using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public enum MediaItemCategoryRequestType
    {
        DEFAULT, NEWEST_DAY, LAST_DAYS, SHUFFLE_DAYS, NO_CATEGORY, NO_LOCATION, NO_DATE, NO_OTHER
    }

    [Serializable]
    public class MediaItemCategoryRequest : MediaItemRequest
    {
        private List<Category> categories = new List<Category>();

        public int Days = 7;

        private MediaItemCategoryRequestType categoryRequestType;
        public MediaItemCategoryRequestType CategoryRequestType
        {
            set
            {
                this.categoryRequestType = value;
                switch (value)
                {
                    case MediaItemCategoryRequestType.NEWEST_DAY:
                        this.SetNewestCategory();
                        break;

                    case MediaItemCategoryRequestType.LAST_DAYS:
                        this.SetLastCategory();
                        break;

                    case MediaItemCategoryRequestType.SHUFFLE_DAYS:
                        this.SetShuffleCategory();
                        break;
                }
            }

            get
            {
                return this.categoryRequestType;
            }
        }

        public override bool IsValid
        {
            get
            {
                if (this.categoryRequestType == MediaItemCategoryRequestType.NO_DATE
                    || this.categoryRequestType == MediaItemCategoryRequestType.NO_OTHER
                    || this.categoryRequestType == MediaItemCategoryRequestType.NO_LOCATION
                    || this.categoryRequestType == MediaItemCategoryRequestType.NO_CATEGORY)
                {
                    return true;
                }
                else
                {
                    return base.IsValid;
                }
            }
        }

        public override void Refresh()
        {
            this.CategoryRequestType = this.categoryRequestType;
        }

        public Category[] Categories
        {
            get
            {
                return this.categories.ToArray();
            }
        }

        override public string Header
        {
            get
            {
                string header;
                switch (this.categoryRequestType)
                {
                    case MediaItemCategoryRequestType.NO_OTHER:
                        header = "Ohne Sonstige-Kategorie";
                        break;

                    case MediaItemCategoryRequestType.NO_CATEGORY:
                        header = "Ohne Kategorie";
                        break;

                    case MediaItemCategoryRequestType.NO_DATE:
                        header = "Ohne Datums-Kategorie";
                        break;

                    case MediaItemCategoryRequestType.NO_LOCATION:
                        header = "Ohne Orts-Kategorie";
                        break;

                    default:
                        header = (this.categories.Count > 0 ? this.categories[0].Header : String.Empty)
                            + (this.categories.Count > 1 ? " ... " + this.categories.Count + "x" : "");
                        break;
                }

                return this.UserDefinedName == null ? header : this.UserDefinedName;
            }
        }

        override public string Description
        {
            get
            {
                switch (this.categoryRequestType)
                {
                    case MediaItemCategoryRequestType.NO_OTHER:
                        return "Zeigt alle Medien an, die keine Kategorie, höchstens Datum und Ort, haben. Kann über die Suche eingeschränkt werden.";

                    case MediaItemCategoryRequestType.NO_CATEGORY:
                        return "Zeigt alle Medien an, die keine Kategorie besitzen. Kann über die Suche eingeschränkt werden.";

                    case MediaItemCategoryRequestType.NO_DATE:
                        return "Zeigt alle Medien an die noch keine Datums-Kategorie besitzen. Kann über die Suche eingeschränkt werden.";

                    case MediaItemCategoryRequestType.NO_LOCATION:
                        return "Zeigt alle Medien an die noch keine Orts-Kategorie besitzen. Kann über die Suche eingeschränkt werden.";

                    default:
                        categories.Sort();
                        return "Kategorie"
                             + (this.categories.Count > 1 ?
                             (this.RequestType == MediaItemRequestType.INTERSECT ? ", Schnittmenge" : (this.RequestType == MediaItemRequestType.UNION ? ", Vereinigung" : ", Vereinigung einzeln")) 
                             : (this.RequestType == MediaItemRequestType.SINGLE ? ", einzeln" : ", recursiv"))
                             + (this.SearchTokenCombined != null ? " (Global eingeschränkt):\n" : ":\n")
                             + String.Join(",\n", categories);

                }
            }
        }

        public void AddCategory(Category category)
        {
            if (category != null)
                this.categories.Add(category);

            this.IsValid = this.categories.Count > 0;
        }

        public void RemoveCategory(Category category)
        {
            this.categories.Remove(category);
            this.IsValid = this.categories.Count > 0;
        }

        /// <summary>
        /// Ersetzt, falls vorhanden, alle bestehenden Kategorien mit der jüngsten.
        /// </summary>
        private void SetNewestCategory()
        {
            ICollection<Category> allCats = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection;

            var maxDateTime = allCats.Where(x => x.FullPath.StartsWith(MediaBrowserContext.CategoryDiary) && x.IsDate && x.Children.Count == 0)
                 .Select(x => x.Date).Union(new List<DateTime>() { DateTime.MinValue }).Max();

            if (maxDateTime > DateTime.Now.AddDays(-10000))
            {
                this.categories.Clear();
                Category maxDateCategory = allCats.First(x => x.FullPath.StartsWith(MediaBrowserContext.CategoryDiary) && x.IsDate && x.Date == maxDateTime);
                this.RequestType = MediaItemRequestType.INTERSECT;
                this.AddCategory(maxDateCategory);
            }
        }

        /// <summary>
        /// Ersetzt, falls vorhanden, alle bestehenden Kategorien mit den n Tagen zurückliegenden
        /// </summary>
        /// <param name="days"></param>
        private void SetLastCategory()
        {
            ICollection<Category> allCats = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection;
            IEnumerable<Category> lastCategories = allCats
                .Where(x => x.FullPath.StartsWith(MediaBrowserContext.CategoryDiary) && x.IsDate && x.Date > DateTime.Now.AddDays(-this.Days) && x.Children.Count == 0)
                .OrderBy(x => x.Date);

            if (lastCategories.Count() > 0)
            {
                this.categories.Clear();
                this.RequestType = MediaItemRequestType.UNION;

                foreach (Category category in lastCategories)
                {
                    this.AddCategory(category);
                }
            }
        }

        /// <summary>
        /// Ersetzt, falls vorhanden, alle bestehenden Kategorien mit n zufälligen Datumskategorien
        /// </summary>
        /// <param name="days"></param>
        private void SetShuffleCategory()
        {
            ICollection<Category> allCats = MediaBrowser4.MediaBrowserContext.CategoryTreeSingelton.FullCategoryCollection;
            List<Category> allDayCategories;

            if (MediaBrowserContext.SearchTokenGlobal != null
                &&
                ((MediaBrowserContext.SearchTokenGlobal.SearchText1.Length != 0 && MediaBrowserContext.SearchTokenGlobal.SearchText1Category)
                || (MediaBrowserContext.SearchTokenGlobal.SearchText2.Length != 0 && MediaBrowserContext.SearchTokenGlobal.SearchText2Category)))
            {
                if (MediaBrowserContext.SearchTokenGlobal.SearchText2.Length == 0 || !MediaBrowserContext.SearchTokenGlobal.SearchText2Category)
                {
                    allDayCategories = allCats.Where(x => x.FullPath.StartsWith(MediaBrowserContext.CategoryDiary) && x.IsDate && x.Children.Count == 0
                        && x.FullPath.ToLower().Contains(MediaBrowserContext.SearchTokenGlobal.SearchText1.Trim().ToLower())).ToList();
                }
                else if (MediaBrowserContext.SearchTokenGlobal.SearchText1.Length == 0 || !MediaBrowserContext.SearchTokenGlobal.SearchText1Category)
                {
                    allDayCategories = allCats.Where(x => x.FullPath.StartsWith(MediaBrowserContext.CategoryDiary) && x.IsDate && x.Children.Count == 0
                        && x.FullPath.ToLower().Contains(MediaBrowserContext.SearchTokenGlobal.SearchText2.Trim().ToLower())).ToList();
                }
                else
                {
                    allDayCategories = allCats.Where(x => x.FullPath.StartsWith(MediaBrowserContext.CategoryDiary) && x.IsDate && x.Children.Count == 0
                        && x.FullPath.ToLower().Contains(MediaBrowserContext.SearchTokenGlobal.SearchText1.Trim().ToLower())
                        && x.FullPath.ToLower().Contains(MediaBrowserContext.SearchTokenGlobal.SearchText2.Trim().ToLower())).ToList();
                }
            }
            else
            {
                allDayCategories = allCats.Where(x => x.FullPath.StartsWith(MediaBrowserContext.CategoryDiary) && x.IsDate && x.Children.Count == 0).ToList();
            }

            Random rand = new Random();
            List<Category> shuffleCategories = new List<Category>();

            while (shuffleCategories.Count < this.Days && allDayCategories.Count > 0)
            {
                int pos = rand.Next(0, allDayCategories.Count);
                shuffleCategories.Add(allDayCategories[pos]);
                allDayCategories.RemoveAt(pos);
            }

            shuffleCategories.Sort();

            if (shuffleCategories.Count() > 0)
            {
                this.categories.Clear();
                this.RequestType = MediaItemRequestType.UNION;

                foreach (Category category in shuffleCategories)
                {
                    this.AddCategory(category);
                }
            }
        }

        public override bool Equals(object obj)
        {
            MediaItemCategoryRequest other = obj as MediaItemCategoryRequest;

            if (other == null)
                return false;

            if (other.UserDefinedId > 0 && this.UserDefinedId > 0)
            {
                return other.UserDefinedId == this.UserDefinedId;
            }
            else if (!(other.UserDefinedId == 0 && this.UserDefinedId == 0))
            {
                return false;
            }

            if (this.categories.Count != other.categories.Count)
                return false;

            if (this.categoryRequestType == MediaItemCategoryRequestType.NO_DATE
                || this.categoryRequestType == MediaItemCategoryRequestType.NO_OTHER
                  || this.categoryRequestType == MediaItemCategoryRequestType.NO_LOCATION
                  || this.categoryRequestType == MediaItemCategoryRequestType.NO_CATEGORY)
            {
                return this.categoryRequestType == other.categoryRequestType;
            }
            else
            {
                foreach (Category category in this.categories)
                {
                    if (!other.categories.Contains(category))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public override int GetHashCode()
        {
            if (this.categoryRequestType == MediaItemCategoryRequestType.NO_DATE
                  || this.categoryRequestType == MediaItemCategoryRequestType.NO_LOCATION
                  || this.categoryRequestType == MediaItemCategoryRequestType.NO_OTHER
                  || this.categoryRequestType == MediaItemCategoryRequestType.NO_CATEGORY)
            {
                return (int)MediaItemCategoryRequestType.NO_DATE;
            }
            else
            {
                int hash = 0;

                foreach (Category category in this.categories)
                {
                    hash += category.GetHashCode();
                }

                return hash.GetHashCode();
            }
        }

        public override string ToString()
        {
            switch (this.categoryRequestType)
            {
                case MediaItemCategoryRequestType.NO_CATEGORY:
                    return "Alle mit ohne Kategorie";

                case MediaItemCategoryRequestType.NO_OTHER:
                    return "Alle ohne Sonstige";

                case MediaItemCategoryRequestType.NO_DATE:
                    return "Alle ohne Datums-Kategorie";

                case MediaItemCategoryRequestType.NO_LOCATION:
                    return "Alle ohne Orts-Kategorie";

                default:
                    return this.RequestType + ": " + this.categories.Count + "x Categories";
            }
        }

        /// <summary>
        /// Nötig nach dem DeSerialisieren, da da die
        /// Category-Objekte dann unvollständig sind
        /// </summary>
        public void RefreshCategories()
        {
            Category[] categoriesOld = this.Categories;

            this.categories.Clear();

            foreach (Category category in categoriesOld)
                this.AddCategory(MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(category.Id));
        }

        public override MediaItemRequest Clone()
        {
            return (MediaItemCategoryRequest)this.MemberwiseClone();
        }
    }
}