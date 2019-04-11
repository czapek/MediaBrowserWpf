using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public class FolderTree
    {
        private FolderCollection children = new FolderCollection();
        public FolderCollection FullFolderCollection = new FolderCollection();

        public FolderCollection Children
        {
            get
            {
                return children;
            }
        }

        public FolderTree(List<Folder> folderList)
        {
            List<Folder> virtualFolderList = new List<Folder>();

            foreach (Folder folder in folderList)
            {
                folder.FolderTree = this;
                this.AddToTree(folder.FullPath.Replace("\\\\", "<<"), null, this.Children, folder, ref virtualFolderList);
            }

            foreach (Folder folder in this.Children)
            {
                folder.name = folder.name.Replace("<<", "\\\\");
            }

            folderList.AddRange(virtualFolderList);
            this.FullFolderCollection = new FolderCollection(folderList.OrderBy(x => x.FullPath));
        }

        public Folder GetFolderById(int id)
        {
            return this.FullFolderCollection.FirstOrDefault(x => x.Id == id);
        }

        public Folder GetFolderByPath(string path)
        {
            return this.FullFolderCollection.FirstOrDefault(x => x.FullPath.Equals(path, StringComparison.InvariantCultureIgnoreCase));
        }

        public void Remove(Folder folder)
        {
            if (folder.Parent == null)
            {
                this.children.Remove(folder);
            }
            else
            {
                folder.Parent.Children.Remove(folder);
            }

            this.FullFolderCollection.Remove(folder);
        }

        private void AddToTree(string parts, Folder parent, FolderCollection folderCollection, Folder folder, ref List<Folder> virtualFolderList)
        {
            Folder foundNode = null;
            string segment = parts.Trim('\\').Split('\\')[0]; //erstes Segment abschneiden

            foreach (Folder node in folderCollection) //schauen ob es das schon im Tree gibt
            {
                if (node.name.ToLower() == segment.ToLower())
                {
                    foundNode = node;
                    break;
                }
            }

            parts = parts.Trim('\\').Substring(segment.Length).Trim('\\'); //wenn hier noch was ist dann ist nächste Ebene tiefer vorhanden ...

            if (foundNode == null)
            {
                if (parts.Length > 0)
                {
                    // wenn nicht gefunden neu anlegen
                    foundNode = new Folder(segment, parent, this);
                    virtualFolderList.Add(foundNode);
                    folderCollection.Add(foundNode);   
                }
                else
                {
                    //Blattknoten ist der ürsprünglich gefundene
                    folderCollection.Add(folder);
                    folder.parent = parent;
                }
            }

            if (parts.Length > 0)
            {
                AddToTree(parts, foundNode, foundNode.Children, folder, ref virtualFolderList);
            }
        }

        public static string [] GetPathParts(string path)
        {
            path = path.TrimEnd('\\');
            if (path.StartsWith("\\\\"))
            {
                path = path.Replace("\\\\", "<<");
                String[] split = path.Split('\\');
                split[0] = split[0].Replace("<<", "\\\\");
                return split;
            }
            else
            {
                path = path.TrimStart('\\');
                return path.Split('\\');
            }
        }
    }
}
