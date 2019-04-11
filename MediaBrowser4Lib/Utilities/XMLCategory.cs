using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data;
using System.Xml;
using System.Xml.Schema;
using System.Globalization;


namespace MediaBrowser4.Utilities
{
    public class XMLCategory
    {
        

        public static List<MediaBrowser4.Objects.Category> GetTreeFromXML(string xmlFilePath)
        {
            System.Xml.XmlDocument xdoc = new System.Xml.XmlDocument();
            xdoc.Load(xmlFilePath);
            return MediaBrowser4.Utilities.XMLCategory.GetTreeFromXML(xdoc);
        }

        public static List<MediaBrowser4.Objects.Category> GetTreeFromXML(XmlDocument xdoc)
        {
            System.Globalization.CultureInfo ciOld = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Globalization.CultureInfo ciUS = new System.Globalization.CultureInfo("en-US");
            System.Threading.Thread.CurrentThread.CurrentCulture = ciUS;

            List<MediaBrowser4.Objects.Category> categoryList = new List<MediaBrowser4.Objects.Category>();
            XmlNodeList nodeList = xdoc.GetElementsByTagName("categories");

            if (nodeList.Count == 1)
            {
                XmlNode catRoot = nodeList[0];

                foreach (XmlNode xmlNode in catRoot.ChildNodes)
                {

                    MediaBrowser4.Objects.Category category = new MediaBrowser4.Objects.Category();
                    category.Name = xmlNode.Attributes["name"].InnerText;
                    category.Description = xmlNode.Attributes["desc"].InnerText;
                    category.Id = Convert.ToInt32(xmlNode.Attributes["id"].InnerText);
                    category.Guid = xmlNode.Attributes["guid"].InnerText;
                    category.Sortname = xmlNode.Attributes["sortname"].InnerText;
                    category.ParentId = 0;
                    categoryList.Add(category);

                    buildTreeRecursive(category, xmlNode);
                }
            }

            System.Threading.Thread.CurrentThread.CurrentCulture = ciOld;

            return categoryList;
        }

        private static void buildTreeRecursive(MediaBrowser4.Objects.Category categoryParent, XmlNode xmlNode2)
        {
            foreach (XmlNode xmlNode in xmlNode2.ChildNodes)
            {
                MediaBrowser4.Objects.Category category = new MediaBrowser4.Objects.Category();
                category.Name = xmlNode.Attributes["name"].InnerText;
                category.Sortname = xmlNode.Attributes["sortname"].InnerText;
                category.Description = xmlNode.Attributes["desc"].InnerText;
                category.Id = Convert.ToInt32(xmlNode.Attributes["id"].InnerText);
                category.Guid = xmlNode.Attributes["guid"].InnerText;
                category.Parent = categoryParent;
                category.ParentId = categoryParent.Id;
                categoryParent.Children.Add(category);

                buildTreeRecursive(category, xmlNode);
            }
        }
    }
}
