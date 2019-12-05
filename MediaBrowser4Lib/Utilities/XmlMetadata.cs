using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using MediaBrowser4.Objects;
using System.IO;
using System.Linq;

namespace MediaBrowser4.Utilities
{
    public class XmlMetadata
    {
        public event EventHandler ExportMessage;
        public String Message;

        public void Import(string xmlPath, Folder folder)
        {
            this.Message = "Importiere ...";
            if (this.ExportMessage != null)
                this.ExportMessage.Invoke(this, EventArgs.Empty);


            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);

            this.Message = "Importiere Struktur";
            if (this.ExportMessage != null)
                this.ExportMessage.Invoke(this, EventArgs.Empty);

            CategoryCollection.SuppressNotification = true;
            List<Category> categoryImportList = this.CreateCategories(xmlDoc);
            CategoryCollection.SuppressNotification = false;

            foreach (XmlNode roleNode in xmlDoc["mediabrowser"]["roles"].ChildNodes)
            {
                Role role = MediaBrowserContext.AllRoles.FirstOrDefault(x => x.Name == roleNode.InnerText);

                if (role == null)
                {
                    role = new Role(0, roleNode.InnerText, null, null);
                }

                foreach (XmlNode mediaItemNode in xmlDoc["mediabrowser"]["mediaitems"].ChildNodes)
                {
                    if (mediaItemNode.Attributes["roleid"] != null)
                    {
                        mediaItemNode.Attributes["roleid"].Value
                            = mediaItemNode.Attributes["roleid"].Value
                            .Replace(roleNode.Attributes["id"].Value, role.Id.ToString());
                    }
                }
            }

            Dictionary<string, Description> descriptionDic = new Dictionary<string, Description>();
            foreach (XmlNode descriptionNode in xmlDoc["mediabrowser"]["descriptions"].ChildNodes)
            {
                descriptionDic.Add(descriptionNode.Attributes["id"].Value, new Description()
                {
                    Command = DescriptionCommand.SET_AND_CREATE_DESCRIPTION,
                    Value = descriptionNode.InnerText,
                    MediaItemList = new List<MediaItem>()
                });
            }

            XmlNodeList xmlMediaItems = xmlDoc.GetElementsByTagName("mediaitem");

            int cnt = 0;
            List<XmlNode> nodeList = new List<XmlNode>();
            foreach (XmlNode node in xmlMediaItems)
            {
                cnt++;
                this.Message = "Importiere " + cnt + " von " + xmlMediaItems.Count;
                if (this.ExportMessage != null)
                    this.ExportMessage.Invoke(this, EventArgs.Empty);

                if (cnt % 50 == 0)
                {
                    CreateMediaItemFromXml(folder, categoryImportList, descriptionDic, nodeList);

                    nodeList = new List<XmlNode>() { node };
                }
                else
                {
                    nodeList.Add(node);
                }
            }

            CreateMediaItemFromXml(folder, categoryImportList, descriptionDic, nodeList);

            foreach (Description desc in descriptionDic.Values)
            {
                MediaBrowserContext.SetDescription(desc);
            }
        }

        private static void CreateMediaItemFromXml(Folder folder, List<Category> categoryImportList, Dictionary<string, Description> descriptionDic, List<XmlNode> nodeList)
        {
            Dictionary<MediaItem, XmlNode> resultList = MediaBrowserContext.CreateMediaItemFromXml(nodeList, folder, categoryImportList);

            foreach (KeyValuePair<MediaItem, XmlNode> kv in resultList)
            {
                if (kv.Value.Attributes["descriptionid"] != null)
                    descriptionDic[kv.Value.Attributes["descriptionid"].Value].MediaItemList.Add(kv.Key);
            }
        }

        private List<Category> CreateCategories(XmlDocument xmlDoc)
        {
            List<Category> categoryImportList = new List<Category>();

            foreach (XmlNode categoryNode in xmlDoc["mediabrowser"]["categories"].ChildNodes)
            {
                Category category = MediaBrowserContext.SetCategoryByPath(categoryNode.InnerText, (categoryNode.Attributes["sortpath"].Value + "\\" + categoryNode.Attributes["sortname"].Value).TrimStart('\\'));

                if (category != null)
                {
                    category.XmlId = Int32.Parse(categoryNode.Attributes["id"].Value);

                    category.Description = categoryNode.Attributes["description"].Value;

                    category.IsLocation = categoryNode.Attributes["latitude"] != null;

                    if (category.IsLocation)
                    {
                        category.Latitude = XmlConvert.ToDouble(categoryNode.Attributes["latitude"].Value);
                        category.Longitude = XmlConvert.ToDouble(categoryNode.Attributes["longitude"].Value);
                    }

                    category.IsDate = categoryNode.Attributes["date"] != null;

                    if (category.IsDate)
                    {
                        category.Date = XmlConvert.ToDateTime(categoryNode.Attributes["date"].Value, "o");
                    }

                    category.IsUnique = XmlConvert.ToBoolean(categoryNode.Attributes["isunique"].Value);

                    MediaBrowserContext.SetCategory(category);

                    categoryImportList.Add(category);
                }
            }

            return categoryImportList;
        }

        public XmlDocument Export(List<MediaBrowser4.Objects.MediaItem> mList, bool full)
        {
            this.Message = "Exportiere ...";
            if (this.ExportMessage != null)
                this.ExportMessage.Invoke(this, EventArgs.Empty);

            XmlDocument xmlDoc = new XmlDocument();

            XmlNode docNode = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDoc.AppendChild(docNode);
            XmlNode rootNodeXML = xmlDoc.CreateElement("mediabrowser");
            XmlAttribute dbGuid = xmlDoc.CreateAttribute("guid");
            XmlAttribute dbName = xmlDoc.CreateAttribute("name");
            XmlAttribute dbUser = xmlDoc.CreateAttribute("user");
            XmlAttribute dbHost = xmlDoc.CreateAttribute("host");
            XmlAttribute dbPath = xmlDoc.CreateAttribute("path");

            dbGuid.Value = MediaBrowserContext.DBGuid;
            dbName.Value = Path.GetFileNameWithoutExtension(MediaBrowserContext.DBName);
            dbUser.Value = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            dbHost.Value = System.Environment.MachineName;
            dbPath.Value = Path.GetDirectoryName(MediaBrowserContext.DBPath);

            rootNodeXML.Attributes.Append(dbGuid);
            rootNodeXML.Attributes.Append(dbName);
            rootNodeXML.Attributes.Append(dbUser);
            rootNodeXML.Attributes.Append(dbHost);
            rootNodeXML.Attributes.Append(dbPath);
            xmlDoc.AppendChild(rootNodeXML);

            XmlNode medNodeRootXML = xmlDoc.CreateElement("mediaitems");
            rootNodeXML.AppendChild(medNodeRootXML);

            Dictionary<int, MediaBrowser4.Objects.Role> roleDic = new Dictionary<int, MediaBrowser4.Objects.Role>();
            foreach (MediaBrowser4.Objects.Role role in MediaBrowserContext.AllRoles)
            {
                roleDic.Add(role.Id, role);
            }

            Dictionary<int, Category> categories = new Dictionary<int, Category>();
            Dictionary<int, string> folders = new Dictionary<int, string>();
            int cnt = 0;
            foreach (MediaBrowser4.Objects.MediaItem mItem in mList)
            {
                cnt++;
                this.Message = "Schreibe " + cnt + " von " + mList.Count;
                if (this.ExportMessage != null)
                    this.ExportMessage.Invoke(this, EventArgs.Empty);

                XmlNode medNode = xmlDoc.CreateElement("mediaitem");

                XmlAttribute medID = xmlDoc.CreateAttribute("id");
                XmlAttribute medName = xmlDoc.CreateAttribute("name");
                XmlAttribute medMD5 = xmlDoc.CreateAttribute("md5");
                XmlAttribute medLENGTH = xmlDoc.CreateAttribute("length");
                XmlAttribute medVIEWED = xmlDoc.CreateAttribute("viewed");
                XmlAttribute medSORTNAME = xmlDoc.CreateAttribute("sortname");
                XmlAttribute medROLE = xmlDoc.CreateAttribute("roleid");
                XmlAttribute medCREATIONDATE = xmlDoc.CreateAttribute("creationdate");
                XmlAttribute medEDITDATE = xmlDoc.CreateAttribute("editdate");
                XmlAttribute medMEDIADATE = xmlDoc.CreateAttribute("mediadate");
                XmlAttribute medINSERTDATE = xmlDoc.CreateAttribute("insertdate");
                XmlAttribute medORIENTATION = xmlDoc.CreateAttribute("orientation");
                XmlAttribute medTYPE = xmlDoc.CreateAttribute("type");
                XmlAttribute medVARIATION = xmlDoc.CreateAttribute("variation");
                XmlAttribute medDURATION = xmlDoc.CreateAttribute("duration");
                XmlAttribute medFRAMES = xmlDoc.CreateAttribute("frames");
                XmlAttribute medPRIORITY = xmlDoc.CreateAttribute("priority");
                XmlAttribute medWIDTH = xmlDoc.CreateAttribute("width");
                XmlAttribute medHEIGHT = xmlDoc.CreateAttribute("height");
                XmlAttribute medFOLDERID = xmlDoc.CreateAttribute("folderid");
                XmlAttribute medDESCRIPTIONID = xmlDoc.CreateAttribute("descriptionid");

                medID.Value = mItem.Id.ToString();
                medName.Value = mItem.FileObject.Name;
                medMD5.Value = mItem.Md5Value;
                medTYPE.Value = mItem.DBType;
                medLENGTH.Value = mItem.FileLength.ToString();
                medVIEWED.Value = mItem.Viewed.ToString();
                medSORTNAME.Value = mItem.Sortorder;
                medROLE.Value = mItem.RoleId.ToString();

                medFOLDERID.Value = mItem.FolderId.ToString();

                if (!folders.ContainsKey(mItem.FolderId))
                {
                    folders.Add(mItem.FolderId, mItem.Foldername);
                }

                medCREATIONDATE.Value = mItem.CreationDate.ToString("o");
                medEDITDATE.Value = mItem.LastWriteDate.ToString("o");
                medMEDIADATE.Value = mItem.MediaDate.ToString("o");
                medINSERTDATE.Value = mItem.InsertDate.ToString("o");

                medORIENTATION.Value = ((int)mItem.Orientation).ToString();
                medVARIATION.Value = mItem.VariationId.ToString();
                medDURATION.Value = XmlConvert.ToString(mItem.Duration);
                medFRAMES.Value = mItem.Frames.ToString();
                medPRIORITY.Value = mItem.Priority.ToString();
                medWIDTH.Value = mItem.Width.ToString();
                medHEIGHT.Value = mItem.Height.ToString();


                medNode.Attributes.Append(medID);
                medNode.Attributes.Append(medName);
                medNode.Attributes.Append(medTYPE);
                medNode.Attributes.Append(medMD5);
                medNode.Attributes.Append(medLENGTH);
                medNode.Attributes.Append(medVIEWED);
                medNode.Attributes.Append(medSORTNAME);
                medNode.Attributes.Append(medROLE);
                medNode.Attributes.Append(medCREATIONDATE);
                medNode.Attributes.Append(medEDITDATE);
                medNode.Attributes.Append(medMEDIADATE);
                medNode.Attributes.Append(medINSERTDATE);
                medNode.Attributes.Append(medORIENTATION);
                medNode.Attributes.Append(medVARIATION);
                medNode.Attributes.Append(medDURATION);
                medNode.Attributes.Append(medFRAMES);
                medNode.Attributes.Append(medPRIORITY);
                medNode.Attributes.Append(medWIDTH);
                medNode.Attributes.Append(medHEIGHT);

                if (mItem.DescriptionId.HasValue)
                {
                    medDESCRIPTIONID.Value = mItem.DescriptionId.ToString();
                    medNode.Attributes.Append(medDESCRIPTIONID);
                }

                medNode.Attributes.Append(medFOLDERID);
                medNodeRootXML.AppendChild(medNode);

                List<Variation> varList = MediaBrowserContext.GetVariations(mItem);

                XmlNode variationsNode = xmlDoc.CreateElement("variations");
                medNode.AppendChild(variationsNode);

                foreach (Variation var in varList)
                {
                    XmlNode variationNode = xmlDoc.CreateElement("variation");

                    XmlAttribute variationID = xmlDoc.CreateAttribute("id");
                    XmlAttribute variationName = xmlDoc.CreateAttribute("name");
                    XmlAttribute variationPosition = xmlDoc.CreateAttribute("position");
                    XmlAttribute variationDescription = xmlDoc.CreateAttribute("description");

                    variationID.Value = var.Id.ToString();
                    variationName.Value = var.Name;
                    variationPosition.Value = var.Position.ToString();

                    if (var.Description != null && var.Description.Trim().Length > 0)
                        variationDescription.Value = var.Description.Trim();

                    variationNode.Attributes.Append(variationID);
                    variationNode.Attributes.Append(variationPosition);
                    variationNode.Attributes.Append(variationName);

                    List<Category> catList = MediaBrowserContext.GetCategories(var.Id);

                    if (catList.Count > 0)
                    {
                        XmlAttribute variationCategories = xmlDoc.CreateAttribute("categoryids");
                        variationCategories.Value = String.Join(",", catList.Select(x => x.Id.ToString()));
                        variationNode.Attributes.Append(variationCategories);

                        foreach (Category cat in catList)
                        {
                            if (!categories.ContainsKey(cat.Id))
                            {
                                categories.Add(cat.Id, cat);
                            }
                        }
                    }

                    variationsNode.AppendChild(variationNode);
                    XmlNode layersNode = xmlDoc.CreateElement("layers");
                    variationNode.AppendChild(layersNode);

                    foreach (Layer layer in var.Layers)
                    {
                        XmlNode layerNode = xmlDoc.CreateElement("layer");
                        layersNode.AppendChild(layerNode);

                        XmlAttribute layerAction = xmlDoc.CreateAttribute("action");
                        layerAction.Value = layer.Action;
                        layerNode.Attributes.Append(layerAction);

                        XmlAttribute layerEdit = xmlDoc.CreateAttribute("edit");
                        layerEdit.Value = layer.Edit;
                        layerNode.Attributes.Append(layerEdit);

                        XmlAttribute layerPosition = xmlDoc.CreateAttribute("position");
                        layerPosition.Value = layer.Position.ToString();
                        layerNode.Attributes.Append(layerPosition);
                    }

                    if (full)
                    {
                        XmlNode variationThumb = xmlDoc.CreateElement("thumbnail");

                        if (var.Id == mItem.VariationId)
                            var.ThumbJpegData = mItem.ThumbJpegData;

                        if (var.ThumbJpegData == null)
                            var.ThumbJpegData = MediaBrowserContext.GetThumbJpegData(var.Id);
                        variationThumb.InnerText = Convert.ToBase64String(var.ThumbJpegData);
                        variationNode.AppendChild(variationThumb);
                    }
                }

                if (full)
                {
                    XmlNode metaNode = xmlDoc.CreateElement("metadata");
                    medNode.AppendChild(metaNode);

                    foreach (MediaBrowser4.Objects.MetaData key in mItem.MetaData)
                    {
                        XmlNode dataNode = xmlDoc.CreateElement("data");
                        XmlAttribute dataKey = xmlDoc.CreateAttribute("key");
                        XmlAttribute dataType = xmlDoc.CreateAttribute("type");
                        XmlAttribute dataGroup = xmlDoc.CreateAttribute("group");
                        dataKey.Value = key.Name.Trim();
                        dataType.Value = key.Type.Trim();
                        dataGroup.Value = key.GroupName.Trim();
                        dataNode.Attributes.Append(dataKey);
                        dataNode.Attributes.Append(dataType);
                        dataNode.Attributes.Append(dataGroup);
                        dataNode.InnerText = key.Value.Trim();
                        metaNode.AppendChild(dataNode);
                    }
                }
            }

            this.Message = "Schreibe Struktur";
            if (this.ExportMessage != null)
                this.ExportMessage.Invoke(this, EventArgs.Empty);


            XmlNode medNodeRoles = xmlDoc.CreateElement("roles");
            rootNodeXML.AppendChild(medNodeRoles);

            foreach (Role role in MediaBrowserContext.AllRoles)
            {
                if (mList.FirstOrDefault(x => x.RoleId == role.Id) != null)
                {
                    XmlNode dataNode = xmlDoc.CreateElement("role");
                    XmlAttribute dataKey = xmlDoc.CreateAttribute("id");

                    dataKey.Value = role.Id.ToString();
                    dataNode.Attributes.Append(dataKey);
                    dataNode.InnerText = role.Name;
                    medNodeRoles.AppendChild(dataNode);
                }
            }

            XmlNode medNodeDescriptions = xmlDoc.CreateElement("descriptions");
            rootNodeXML.AppendChild(medNodeDescriptions);

            foreach (Description desc in MediaBrowserContext.GetDescription(mList))
            {
                XmlNode dataNode = xmlDoc.CreateElement("description");
                XmlAttribute dataKey = xmlDoc.CreateAttribute("id");
                dataKey.Value = desc.Id.ToString();
                dataNode.Attributes.Append(dataKey);
                dataNode.InnerText = desc.Value;
                medNodeDescriptions.AppendChild(dataNode);
            }

            XmlNode medNodeFolders = xmlDoc.CreateElement("folders");
            rootNodeXML.AppendChild(medNodeFolders);

            foreach (KeyValuePair<int, string> kv in folders)
            {
                XmlNode dataNode = xmlDoc.CreateElement("folder");
                XmlAttribute dataKey = xmlDoc.CreateAttribute("id");
                dataKey.Value = kv.Key.ToString();
                dataNode.Attributes.Append(dataKey);
                dataNode.InnerText = kv.Value;
                medNodeFolders.AppendChild(dataNode);
            }

            XmlNode medNodeCategories = xmlDoc.CreateElement("categories");
            rootNodeXML.AppendChild(medNodeCategories);

            foreach (KeyValuePair<int, Category> cat in categories)
            {
                XmlNode categorizeNode = xmlDoc.CreateElement("category");

                XmlAttribute categorizeID = xmlDoc.CreateAttribute("id");
                categorizeID.Value = cat.Value.Id.ToString();
                categorizeNode.Attributes.Append(categorizeID);

                XmlAttribute categorizeName = xmlDoc.CreateAttribute("name");
                categorizeName.Value = cat.Value.Name;
                categorizeNode.Attributes.Append(categorizeName);

                XmlAttribute categorizeIsUnique = xmlDoc.CreateAttribute("isunique");
                categorizeIsUnique.Value = XmlConvert.ToString(cat.Value.IsUnique);
                categorizeNode.Attributes.Append(categorizeIsUnique);

                if (cat.Value.IsLocation && cat.Value.Latitude.HasValue)
                {
                    XmlAttribute categorizeLatitude = xmlDoc.CreateAttribute("latitude");
                    categorizeLatitude.Value = XmlConvert.ToString(cat.Value.Latitude.Value);
                    categorizeNode.Attributes.Append(categorizeLatitude);

                    XmlAttribute categorizeLongitude = xmlDoc.CreateAttribute("longitude");
                    categorizeLongitude.Value = XmlConvert.ToString(cat.Value.Longitude.Value);
                    categorizeNode.Attributes.Append(categorizeLongitude);
                }

                if (cat.Value.IsDate)
                {
                    XmlAttribute categorizeDate = xmlDoc.CreateAttribute("date");
                    categorizeDate.Value = cat.Value.Date.ToString("o");
                    categorizeNode.Attributes.Append(categorizeDate);
                }

                XmlAttribute categorizeSortname = xmlDoc.CreateAttribute("sortname");
                categorizeSortname.Value = cat.Value.Sortname;
                categorizeNode.Attributes.Append(categorizeSortname);

                XmlAttribute categorizeDescription = xmlDoc.CreateAttribute("description");
                categorizeDescription.Value = cat.Value.Description;
                categorizeNode.Attributes.Append(categorizeDescription);

                XmlAttribute categorizeSortpath = xmlDoc.CreateAttribute("sortpath");
                categorizeSortpath.Value = cat.Value.SortPath.TrimStart('\\');
                categorizeNode.Attributes.Append(categorizeSortpath);

                categorizeNode.InnerText = cat.Value.FullPath.TrimStart('\\');

                medNodeCategories.AppendChild(categorizeNode);
            }

            return xmlDoc;
        }
    }
}
