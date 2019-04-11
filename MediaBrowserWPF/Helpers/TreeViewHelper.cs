using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using MediaBrowser4.Objects;
using System.Windows.Threading;

namespace MediaBrowserWPF.Helpers
{
    public class TreeViewHelper
    {
        private TreeView treeViewMain;

        private Dictionary<ItemContainerGenerator, ITreeNode> nodeExpandDictionary = new Dictionary<ItemContainerGenerator, ITreeNode>();
        private Dictionary<ItemContainerGenerator, string> pathExpandDictionary = new Dictionary<ItemContainerGenerator, string>();

        public TreeViewHelper(TreeView treeView)
        {
            this.treeViewMain = treeView;
        }

        public bool IsCollapsed
        {
            get
            {     
                for (int i = 0; i < this.treeViewMain.Items.Count; i++)
                {
                    if (((TreeViewItem)this.treeViewMain.ItemContainerGenerator.ContainerFromIndex(i)).IsExpanded)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void CollapseAll()
        {
            for (int i = 0; i < this.treeViewMain.Items.Count; i++)
            {
                this.Collapse((TreeViewItem)this.treeViewMain.ItemContainerGenerator.ContainerFromIndex(i));
            }
        }

        public void Collapse(TreeViewItem treeViewItemRoot)
        {
            if (treeViewItemRoot == null)
                return;

            treeViewItemRoot.IsExpanded = false;

            for (int i = 0; i < treeViewItemRoot.Items.Count; i++)
            {
                this.Collapse((TreeViewItem)treeViewItemRoot.ItemContainerGenerator.ContainerFromIndex(i));
            }
        }

        public void ExpandPath(string path)
        {
            if (path == null || path.Trim().Length == 0) return;
            this.ExpandPath(MediaBrowser4.Utilities.FilesAndFolders.CleanPath(path) + "\\", this.treeViewMain.ItemContainerGenerator, this.treeViewMain.Items);
            this.treeViewMain.Focus();
        }

        private void ExpandPath(string path, ItemContainerGenerator itemContainerGenerator, ItemCollection children)
        {
            try
            {
                string part = null;

                if (path.StartsWith("\\\\"))
                {
                    part = "\\\\" + path.Substring(2).Split('\\')[0];
                }
                else
                {
                    part = path.Split('\\')[0];
                }

                foreach (ITreeNode node in children)
                {
                    if (node.Header.TrimEnd('\\').Equals(part, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (itemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                        {
                            TreeViewItem treeViewItem = (TreeViewItem)itemContainerGenerator.ContainerFromItem(node);
                            this.ExpandTreeViewItem(treeViewItem);

                            this.ExpandPath(path.Substring(part.Length + 1, path.Length - (part.Length + 1)), treeViewItem.ItemContainerGenerator, treeViewItem.Items);
                        }
                        else
                        {
                            pathExpandDictionary[itemContainerGenerator] = path;
                            nodeExpandDictionary[itemContainerGenerator] = node;
                            itemContainerGenerator.StatusChanged += new EventHandler(ItemContainerGenerator_StatusChanged);
                        }

                        break;
                    }
                }
            }
            catch { }
        }

        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            ItemContainerGenerator itemContainerGenerator = (ItemContainerGenerator)sender;

            if (itemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated
                && this.pathExpandDictionary.ContainsKey(itemContainerGenerator)
                && this.nodeExpandDictionary.ContainsKey(itemContainerGenerator))
            {
                TreeViewItem treeViewItem = itemContainerGenerator.ContainerFromItem(this.nodeExpandDictionary[itemContainerGenerator]) as TreeViewItem;

                if (treeViewItem == null)
                    return;

                this.ExpandTreeViewItem(treeViewItem);

                string part = null;
                if (this.pathExpandDictionary[itemContainerGenerator].StartsWith("\\\\"))
                {
                    part = "\\\\" + this.pathExpandDictionary[itemContainerGenerator].Substring(2).Split('\\')[0];
                }
                else
                {
                    part = this.pathExpandDictionary[itemContainerGenerator].Split('\\')[0];
                }

                this.ExpandPath(this.pathExpandDictionary[itemContainerGenerator].Substring(part.Length + 1), treeViewItem.ItemContainerGenerator, treeViewItem.Items);
            }
        }

        private void ExpandTreeViewItem(TreeViewItem treeViewItem)
        {
            if (treeViewItem == null)
                return;

            treeViewItem.IsExpanded = true;
           // treeViewItem.IsSelected = true;      
            treeViewItem.BringIntoView();
        }
    }
}
