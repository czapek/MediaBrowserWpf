using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MediaBrowser4.Objects;
using MediaBrowserWPF.UserControls.CategoryContainer;

namespace MediaBrowserWPF.Dialogs
{
    /// <summary>
    /// Interaktionslogik für Tag.xaml
    /// </summary>
    public partial class Tag : UserControl
    {
        public event EventHandler RemoveCategory;
        public Category Category
        {
            private set;
            get;
        }

        public int Count { get; set; }

        public Tag()
        {
            InitializeComponent();
        }

        public Tag(Category category, int count)
        {
            InitializeComponent();

            this.Count = count;
            this.Category = category;
            this.TagLabel.Content = category.Name + "  X";
            this.TagLabel.ToolTip = (count > 0 ? count + " x " : String.Empty) + category.FullName;            
        }
        
        private void TagLabel_Click(object sender, RoutedEventArgs e)
        {
            if (this.RemoveCategory != null)
                this.RemoveCategory.Invoke(this, EventArgs.Empty);

        }

        public override string ToString()
        {
            return this.Category.ToString();
        }     
     

        public bool Equals(Category category)
        {
            if (category.Id == 0 || this.Category.Id == 0)
            {
                return category.ToString().Equals(this.ToString());
            }

            return this.Category.Id == category.Id;
        }

        public bool Equals(String fullname)
        {
            return this.Category.FullPath == fullname;
        }
    }
}
