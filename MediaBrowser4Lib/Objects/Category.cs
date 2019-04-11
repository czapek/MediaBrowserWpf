using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;

namespace MediaBrowser4.Objects
{
    [Serializable]
    public class Category : ITreeNode, IComparable, INotifyPropertyChanged
    {
        internal string name;

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        public Category()
        {
        }

        public Category(CategoryTree categoryTree)
        {
            this.categoryTree = categoryTree;
        }

        public string Header
        {
            get { return this.Name; }
        }


        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                this.fullpath = null;
                OnPropertyChanged("ToString");
                OnPropertyChanged("FullName");
                OnPropertyChanged("Name");
            }
        }

        public string Description
        {
            get;
            set;
        }

        public string Sortname
        {
            get;
            set;
        }

        public string FullName
        {
            get
            {
                return this.ToString();
            }

            set
            {
            }
        }

        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }

        [NonSerialized]
        public int XmlId;

        [NonSerialized]
        private Category parent;
        public Category Parent
        {
            get
            {
                return this.parent;
            }

            set
            {
                if (this.parent == value && this.Id > 0)
                    return;

                if (this.parent != null)
                {
                    this.parent.Children.Remove(this);
                }
                else
                {
                    if (this.categoryTree != null)
                        this.categoryTree.Children.Remove(this);
                }

                this.parent = value;
                this.ParentId = value == null ? 0 : value.Id;

                if (value != null)
                {
                    if (!value.Children.Contains(this) && this.Id > 0)
                        value.Children.Add(this);
                }
                else
                {
                    if (!this.categoryTree.Children.Contains(this) && this.Id > 0)
                        this.categoryTree.Children.Add(this);
                }

                OnPropertyChanged("ToString");
                OnPropertyChanged("FullName");
            }
        }

        public Category NextSilbing
        {
            get
            {
                Category cat = null;

                if (this.Parent != null && this.Parent.Children != null)
                {
                    for (int i = 0; i < this.Parent.Children.Count; i++)
                    {
                        if (this.Parent.Children[i] == this && this.Parent.Children.Count > i + 1)
                        {
                            return this.Parent.Children[i + 1];
                        }
                    }
                }

                return cat;
            }
        }

        public string NameDate
        {
            get
            {
                if (this.IsDate)
                {
                    return this.Date.ToShortDateString();
                }
                else
                {
                    return this.Name;
                }
            }
        }

        public int Id
        {
            get;
            set;
        }

        public string Guid
        {
            get;
            set;
        }

        public int ParentId
        {
            get;
            set;
        }

        public int ItemCount
        {
            get;
            set;
        }

        [NonSerialized]
        public decimal Longitude, Latitude;
        public DateTime Date;
        public bool IsUnique;
        public bool IsUniqueChanged = false;
        public bool IsDateChanged = false;
        public bool IsLocationChanged = false;
        [NonSerialized]
        private CategoryCollection children;
        public bool IsLocation, IsDate;

        private Category uniqueRoot;
        public Category UniqueRoot
        {
            get
            {
                if (this.uniqueRoot == null && this.IsUnique)
                {
                    Category parent = this.Parent;
                    this.uniqueRoot = this;
                    while (parent != null && parent.IsUnique)
                    {
                        this.uniqueRoot = parent;
                        parent = parent.Parent;
                    }
                }

                return this.uniqueRoot;
            }
        }

        [NonSerialized]
        private CategoryTree categoryTree;
        public CategoryTree CategoryTree
        {
            get { return this.categoryTree; }
            internal set { this.categoryTree = value; }
        }

        public CategoryCollection Children
        {
            get
            {
                if (children == null)
                {
                    children = new CategoryCollection();
                }
                return children;
            }
        }

        public int ChildrenCount
        {
            get
            {
                return Children.Count;
            }
        }

        public int ItemCountRecursive
        {
            get
            {
                int cnt = this.ItemCount;

                ChildrenRecursiveCount(this, ref cnt);

                return cnt;
            }
        }

        public void UpdateItemInfo()
        {
            this.OnPropertyChanged("ItemCount");
            this.OnPropertyChanged("ItemCountRecursive");
        }

        private void ChildrenRecursiveCount(Category folder, ref int cnt)
        {
            foreach (Category children in folder.Children)
            {
                cnt += children.ItemCount;

                ChildrenRecursiveCount(children, ref cnt);
            }
        }


        /// <summary>
        /// Alle Knoten auf diese Ebene
        /// </summary>
        public CategoryCollection Silbings
        {
            get
            {
                if (this.Parent != null)
                {
                    return this.Parent.Children;
                }
                else
                {
                    return this.CategoryTree == null ? new CategoryCollection() : this.CategoryTree.Children;
                }
            }
        }

        /// <summary>
        /// Kopiert eine Liste alle Knoten auf dieser Ebene ohne diese Kategorie
        /// </summary>
        /// <returns></returns>
        public List<Category> GetSilbingList()
        {
            return this.Silbings.Where(x => x.Id != this.Id).ToList();
        }

        public List<Category> AllChildrenRecursive()
        {
            List<Category> allCats = new List<Category>();
            this.AddCategories(this, allCats);
            return allCats;
        }

        private void AddCategories(Category root, List<Category> allCats)
        {
            allCats.Add(root);
            foreach (Category cat in root.Children)
            {
                this.AddCategories(cat, allCats);
            }
        }

        public void Remove()
        {
            this.Silbings.Remove(this);
        }

        private string fullpath;
        public string FullPath
        {
            get
            {
                if (fullpath == null)
                {
                    fullpath = this.Path + "\\" + this.Name;
                }
                return this.Path + "\\" + this.Name;
            }

            set
            {
                fullpath = value;
            }
        }

        public int Layer
        {
            get
            {
                int layer = 0;
                Category cat = this;

                while (cat.Parent != null)
                {
                    cat = cat.Parent;
                    layer++;
                }
                return layer;
            }
        }

        public string Path
        {
            get
            {
                return String.Join("/", TraverseUp().Select(x => x.name).Reverse());
            }
        }

        public string BreadCrumpReverse
        {
            get
            {
                List<Category> catList = TraverseUp();

                return catList.Count == 0 ? String.Empty : ":: " + String.Join(" < ", catList.Select(x=>x.name));
            }
        }

        private List<Category> TraverseUp()
        {
            List<Category> upPath = new List<Category>();

            Category node = this;

            while (node.ParentId > 0)
            {
                if (node.Parent == null)
                {
                    node = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(node.ParentId);
                    upPath.Add(node);
                }
                else
                {
                    upPath.Add(node.Parent);
                    node = node.Parent;
                }
            }

            return upPath;
        }

        public string SortPath
        {
            get
            {
                string path = "";
                Category node = this;

                while (node.ParentId > 0)
                {
                    if (node.Parent == null)
                    {
                        node = MediaBrowserContext.CategoryTreeSingelton.GetcategoryById(node.ParentId);
                        path = node.Sortname + path;
                    }
                    else
                    {
                        path = node.Parent.Sortname + "\\" + path;
                        node = node.Parent;
                    }
                }

                return path.TrimEnd('\\');
            }
        }


        public static List<Category> BuildFlatList(List<Category> categoryTreeList)
        {
            List<Category> flatList = new List<Category>();

            foreach (Category cat in categoryTreeList)
            {
                addToFlatListRecursive(cat, flatList);
            }

            return flatList;
        }

        private static void addToFlatListRecursive(Category category, List<Category> flatList)
        {
            flatList.Add(category);
            foreach (Category cat in category.Children)
            {
                addToFlatListRecursive(cat, flatList);
            }
        }

        public string ToolTip
        {
            get
            {
                if (this.Description != null && this.Description.Trim().Length > 0)
                {
                    return this.Description;
                }
                else if (this.IsDate)
                {
                    if (this.Layer == 1)
                    {
                        return this.Date.ToString("yyyy");
                    }
                    else if (this.Layer == 2)
                    {
                        return this.Date.ToString("MMMM yyyy");
                    }
                    else if (this.Layer == 3)
                    {
                        return this.Date.ToString("D");
                    }
                    else if (this.Layer == 4)
                    {
                        return this.Date.ToString("f");
                    }
                    else if (this.Layer >= 5)
                    {
                        return this.Date.ToString("F");
                    }
                }

                return this.Path;
            }
        }

        public override string ToString()
        {
            return this.Parent == null ? this.Name : this.Name + " (" + this.Path + ")";
        }

        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == this.GetHashCode();
        }

        public override int GetHashCode()
        {
            return this.Id;
        }

        public int CompareTo(object obj)
        {
            Category category = obj as Category;

            if (category == null)
            {
                return -1;
            }
            else
            {
                if (category.IsDate && this.IsDate)
                {
                    return this.Date.CompareTo(category.Date);
                }
                else if (!category.IsDate && !this.IsDate)
                {
                    return this.FullPath.CompareTo(category.FullPath);
                }
                else
                {
                    return category.IsDate ? -1 : 1;
                }
            }
        }

        // Create the OnPropertyChanged method to raise the event
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
