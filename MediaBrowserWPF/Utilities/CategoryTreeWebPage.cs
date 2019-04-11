using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MediaBrowser4.Objects;
using MediaBrowser4;
using System.ComponentModel;
using System.Drawing;
using System.Net;

namespace MediaBrowserWPF.Utilities
{
    public class CategoryTreeWebPage : IDisposable
    {
        private static bool? isIMP = null;
        public static bool IsIMP
        {
            get
            {
                if (isIMP == null)
                    isIMP = Dns.GetHostName() == "impdt120";

                return (bool)isIMP;
            }
        }

        Category categoryRootNode;
        public CategoryTreeWebPage(Category categoryRootNode)
        {
            if (IsIMP)
            {
                ExportPath = @"\\imps3\fotos_inmediasp$\index.htm";
                CopyFiles = false;
                DisplayFilename = false;
                DisplayIcon = true;
                LimitDescription = 50;
                PreviewSize = new System.Drawing.Size(800, 800);
                UniqueNames = false;
            }

            this.categoryRootNode = categoryRootNode;
        }

        [BrowsableAttribute(false)]
        public string Name
        {
            get { return "Create a webpage with \"" + categoryRootNode.Name + "\" as a root node."; }
        }

        [BrowsableAttribute(false)]
        public string[] PredefinedValues
        {
            get { return null; }
        }

        public void SetByPredefinedValue(int index)
        {

        }

        [BrowsableAttribute(false)]
        public int DefaultPredefinedValue
        {
            get { return 0; }
        }

        bool uniqueNames = false;
        [DescriptionAttribute("All copied mediafiles and their folders will be renamed with an unique id.")]
        public bool UniqueNames
        {
            get { return uniqueNames; }
            set { uniqueNames = value; }
        }

        bool displayFilename = false;
        [DescriptionAttribute("Displays the filename in the webpage under the thumbnail image.")]
        public bool DisplayFilename
        {
            get { return displayFilename; }
            set { displayFilename = value; }
        }

        int limitDescription = 50;
        [DescriptionAttribute("Limit the length of the description under the thumbnail image. Set to zero if no description should be displayed.")]
        public int LimitDescription
        {
            get { return limitDescription; }
            set { limitDescription = value; }
        }

        bool createPreviews = true;
        [DescriptionAttribute("Creates a subfolder named 'preview' with smaller representations of images.")]
        public bool CreatePreviews
        {
            get { return createPreviews; }
            set { createPreviews = value; }
        }

        System.Drawing.Size previewSize = new System.Drawing.Size(800, 800);
        [DescriptionAttribute("Previews of images will only created, if they are bigger then this size.")]
        public System.Drawing.Size PreviewSize
        {
            get { return previewSize; }
            set { previewSize = value; }
        }

        bool copyFiles = true;
        [DescriptionAttribute("Copy media files to the web folder.")]
        public bool CopyFiles
        {
            get { return copyFiles; }
            set
            {
                copyFiles = value;
                if (!copyFiles)
                    UniqueNames = false;
            }
        }

        bool displayIcon = true;
        [DescriptionAttribute("Displays an extra icon for video files.")]
        public bool DisplayIcon
        {
            get { return displayIcon; }
            set { displayIcon = value; }
        }

        string exportPath = (MediaBrowserContext.GetDBProperty("DefaultMediaArchivFolder") + "\\index.htm").Replace("\\\\", "\\");
        public string ExportPath
        {
            get { return exportPath; }
            set { exportPath = value; }
        }

        public event EventHandler<MediaItemCallbackArgs> OnUpdate;

        public void Start()
        {
            BuildTree();

            if (this.OnUpdate != null)
            {
                this.OnUpdate(this, new MediaItemCallbackArgs(100, 100, null, true, false));
            }
        }

        private void AddCatChild(Category cat, StringBuilder sb, ref int pos, bool open)
        {
            if (cat == null)
                return;

            if (open)
            {
                //sb.Append(String.Empty.PadLeft(pos + 1, '\t')
                //    + "<li id=\"cat_" + cat.Id + "\"><a href=\"#\">"
                //    + System.Web.HttpUtility.HtmlEncode(cat.Name.Trim()) + "</a>");

                sb.Append(String.Empty.PadLeft(pos + 1, '\t')
                    + "{ \"id\":" + cat.Id + ", \"parameter\":" + cat.Id + "," + (String.IsNullOrWhiteSpace(cat.Description) ? "" : "\"description\":\"" +
                        System.Web.HttpUtility.HtmlEncode(cat.Description.Trim())
                        + "\",") + " \"name\":\""
                    + System.Web.HttpUtility.HtmlEncode(cat.Name.Trim()) + "\"");

                if (cat.Children.Count == 0)
                {
                    //sb.AppendLine("</li>");
                    sb.AppendLine("}" + (cat.NextSilbing == null ? String.Empty : ","));
                }
                else
                {
                    sb.AppendLine();
                    pos++;
                    //sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + "<ul>");
                    sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + ", \"children\": [");
                }
            }
            else
            {
                // sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + "</ul>");
                sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + "]");
                pos--;
                //sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + "</li>");
                sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + "}" + (cat.NextSilbing == null ? String.Empty : ","));

                Category silbing = cat.NextSilbing;
                if (silbing != null && silbing.Children.Count > 0)
                {
                    //sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t')
                    //   + "<li id=\"cat_" + silbing.Id + "\"><a href=\"#\">"
                    //   + System.Web.HttpUtility.HtmlEncode(silbing.Name.Trim()) + "</a>");

                    sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t')
                        + "{ \"id\":" + silbing.Id + ", \"parameter\":" + silbing.Id + "," + (String.IsNullOrWhiteSpace(silbing.Description) ? "" : "\"description\":\"" +
                        System.Web.HttpUtility.HtmlEncode(silbing.Description.Trim())
                        + "\",") + " \"name\":\"" + System.Web.HttpUtility.HtmlEncode(silbing.Name.Trim()) + "\"");

                    pos++;
                    // sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + "<ul>");
                    sb.AppendLine(String.Empty.PadLeft(pos + 1, '\t') + ", \"children\": [");
                }
            }

            if (cat.Children.Count > 0 && open)
            {
                pos++;
                AddCatChild(cat.Children[0], sb, ref pos, true);
            }
            else
            {
                Category silbing = cat.NextSilbing;
                if (silbing != null && !open && silbing.Children.Count > 0)
                {
                    pos++;
                    AddCatChild(silbing.Children[0], sb, ref pos, true);
                }
                else if (silbing == null)
                {
                    if (pos > 0)
                    {
                        pos--;
                        AddCatChild(cat.Parent, sb, ref pos, false);
                    }
                }
                else
                {
                    AddCatChild(silbing, sb, ref pos, true);
                }
            }
        }

        TakeSnapshot takeSnapshot = new TakeSnapshot();
        public void BuildTree()
        {
            string thumbFolder = "thumbs";
            int colls = 5;

            StringBuilder sb = new StringBuilder();

            string rootFolder = Path.GetDirectoryName(exportPath);
            Dictionary<Category, List<MediaItem>> allLeafsDic = new Dictionary<Category, List<MediaItem>>();
            List<Category> allLeafsList = new List<Category>();
            string resourcesPath = System.Windows.Forms.Application.StartupPath + "\\Resources";

            sb.Append("<html>\n");
            sb.Append("<head>\n");
            sb.Append("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\">\n");
            sb.Append("<title>" + System.Web.HttpUtility.HtmlEncode(categoryRootNode.Name) + "</title>\n");
            sb.Append("<link href=\"thumbs/default.css\" type=\"text/css\" rel=\"stylesheet\">\n");
            sb.Append("<script type=\"text/javascript\" src=\"thumbs/dtree.js\"></script>\n");
            sb.Append("<META HTTP-EQUIV=\"EXPIRES\" CONTENT=\"0\">\n");
            sb.Append("<META HTTP-EQUIV=\"CACHE-CONTROL\" CONTENT=\"NO-CACHE\">\n");
            sb.Append("</head>\n");
            sb.Append("<body>\n");

            sb.Append(File.ReadAllText(resourcesPath + "\\mediabrowser2TreviewA.htm"));

            if (categoryRootNode.Description != null &&
                        (categoryRootNode.Description.ToLower().StartsWith("http://")
                        || categoryRootNode.Description.ToLower().StartsWith("../")))
            {
                sb.Append("d.add(" + categoryRootNode.Id + ",-1,'"
                   + System.Web.HttpUtility.HtmlEncode(categoryRootNode.Name)
                            + "','" + categoryRootNode.Description + "','Generated by Mediabrowser2',''); ");
            }
            else
            {
                sb.Append("d.add(" + categoryRootNode.Id + ",-1,'"
                    + System.Web.HttpUtility.HtmlEncode(categoryRootNode.Name)
                            + "','http://www.czapek.de/MediaBrowser2/index.htm','Created by Mediabrowser2','_blank'); ");
            }

            treeRecursive(sb, categoryRootNode, allLeafsList);


            sb.Append(File.ReadAllText(resourcesPath + "\\mediabrowser2TreviewB.htm"));

            int cnt = 0;
            int allMitemsCount = 0;
            foreach (Category node in allLeafsList)
            {
                cnt++;
                if (this.OnUpdate != null)
                {
                    this.OnUpdate(this, new MediaItemCallbackArgs(cnt, allLeafsList.Count, null, false, false));
                }

                List<MediaItem> mList = MediaBrowserContext.GetMediaItemsFromCategories(
                    new List<Category>() { node }, false, "", 1000000);

                allLeafsDic.Add(node, mList);
                allMitemsCount += mList.Count;
            }

            if (!Directory.Exists(rootFolder + "\\" + thumbFolder))
            {
                Directory.CreateDirectory(rootFolder + "\\" + thumbFolder);
                DirectoryInfo dinf = new DirectoryInfo(rootFolder + "\\" + thumbFolder);
                dinf.Attributes = dinf.Attributes | FileAttributes.Hidden;
            }

            StringBuilder previewSb = new StringBuilder();
            StringBuilder thumbsSb = new StringBuilder();
            StringBuilder catTree = new StringBuilder();

            int posCount = -1;
            this.AddCatChild(categoryRootNode, catTree, ref posCount, true);

            //  File.WriteAllText(Path.Combine(rootFolder, "treeViewData.htm"), "<ul>\r\n" + catTree.ToString() + "\r\n</ul>");
            File.WriteAllText(Path.Combine(rootFolder, "scripts\\treeData.js"), catTree.ToString(), Encoding.UTF8);

            cnt = 0;
            foreach (Category categoryNode in allLeafsList)
            {
                StringBuilder sTable = new StringBuilder();
                List<string> jsonList = new List<string>();
                int collCount = 0;
                string newMediaFileName, newMediaFolderName, previewFilePath;

                if (allLeafsDic[categoryNode].Count > 0)
                {
                    sTable.Append("<table id=\"itemsTable\" class=\"itemsTable\" align=\"center\">\n");

                    sTable.Append("<tr><td class=\"itemsTableHead\" colspan=\"" + colls + "\">\n");
                    sTable.Append(System.Web.HttpUtility.HtmlEncode(categoryNode.Name));
                    sTable.Append("<td></tr>\n");

                    if (categoryNode.Description.Trim().Length > 0)
                    {
                        sTable.Append("<tr><td class=\"itemsTableDesc\" colspan=\"" + colls + "\">\n");
                        sTable.Append(System.Web.HttpUtility.HtmlEncode(categoryNode.Description.Trim()));
                        sTable.Append("<td></tr>\n");
                    }

                    if (categoryNode.Name == "Alle Mitarbeiter")
                    {
                        allLeafsDic[categoryNode].Sort(
                            delegate(MediaItem m1, MediaItem m2)
                            {
                                return
                                    System.IO.Path.GetFileNameWithoutExtension(m1.Filename).Split(' ')[System.IO.Path.GetFileNameWithoutExtension(m1.Filename).Split(' ').Length - 1].CompareTo(
                                    System.IO.Path.GetFileNameWithoutExtension(m2.Filename).Split(' ')[System.IO.Path.GetFileNameWithoutExtension(m2.Filename).Split(' ').Length - 1]);
                            });
                    }
                    else
                    {
                        allLeafsDic[categoryNode].Sort(
                            delegate(MediaItem m1, MediaItem m2) { return m1.Filename.CompareTo(m2.Filename); });
                    }

                    int mCnt = 0;
                    foreach (MediaItem mItem in allLeafsDic[categoryNode])
                    {
                        string thumbCatFolder = rootFolder + "\\" + thumbFolder + "\\folder_" + mItem.FolderId;

                        if (!copyFiles && !mItem.Foldername.ToLower().Trim().StartsWith(rootFolder.ToLower().Trim()))
                            continue;

                        if (!Directory.Exists(thumbCatFolder))
                        {
                            Directory.CreateDirectory(thumbCatFolder);
                        }

                        //store old location
                        mItem.Tag = mItem.FileObject.FullName;

                        if (copyFiles)
                        {
                            newMediaFolderName = rootFolder + "\\" + mItem.FileObject.Directory.Name;
                            newMediaFileName = rootFolder + "\\" + mItem.FileObject.Directory.Name
                                + "\\" + mItem.FileObject.Name;

                            if (uniqueNames)
                            {
                                newMediaFolderName = rootFolder + "\\" + mItem.FolderId.ToString().PadLeft(7, '0');
                                newMediaFileName = rootFolder + "\\" + mItem.FolderId.ToString().PadLeft(7, '0')
                                    + "\\" + mItem.Id.ToString().PadLeft(7, '0') + Path.GetExtension(mItem.Filename);
                            }

                            previewFilePath = newMediaFolderName + "\\preview\\" + Path.GetFileName(newMediaFileName);

                            FileInfo mediaItemFileInfo = new FileInfo(newMediaFileName);

                            if (!Directory.Exists(newMediaFolderName))
                                Directory.CreateDirectory(newMediaFolderName);

                            if (mediaItemFileInfo.Exists && mediaItemFileInfo.Length != mItem.FileLength)
                            {
                                mediaItemFileInfo.Delete();
                            }

                            if (!File.Exists(newMediaFileName))
                                mItem.FileObject.CopyTo(newMediaFileName);

                            mItem.Foldername = newMediaFolderName;
                            mItem.FileObject = new FileInfo(newMediaFileName);
                        }
                        else
                        {
                            previewFilePath = mItem.FileObject.Directory.FullName
                                + "\\preview\\" + mItem.Filename;

                            previewSb.AppendLine(previewFilePath);
                        }

                        if (IsIMP)
                        {
                            if (createPreviews &&
                               (mItem.Width > 1050 || mItem.Height > 1050
                               || mItem.Orientation != MediaItem.MediaOrientation.BOTTOMisBOTTOM
                               || mItem.Layers.Count > 0))
                            {
                                takeSnapshot.ExportImage(mItem, previewFilePath, previewSize.Width, 90, false);
                            }
                        }
                        else
                        {
                            if (createPreviews &&
                                (mItem.Width > previewSize.Width || mItem.Height > previewSize.Height
                                || mItem.Orientation != MediaItem.MediaOrientation.BOTTOMisBOTTOM
                                || mItem.Layers.Count > 0))
                            {
                                takeSnapshot.ExportImage(mItem, previewFilePath, previewSize.Width, 90, false);
                            }
                        }

                        newMediaFolderName = mItem.Foldername.Substring(rootFolder.Length).Trim('\\').Replace('\\', '/') + "/";

                        FileInfo thumbFileInfo = new FileInfo(thumbCatFolder + "\\" + "\\tn_"
                        + (mItem.FileObject.Name.ToLower().EndsWith(".jpg") ? mItem.FileObject.Name : mItem.FileObject.Name + ".jpg"));

                        thumbsSb.AppendLine(thumbFileInfo.FullName);

                        if (mItem.ThumbJpegData != null && (!thumbFileInfo.Exists || thumbFileInfo.Length != mItem.ThumbJpegData.Length))
                            File.WriteAllBytes(thumbFileInfo.FullName, mItem.ThumbJpegData);

                        if (collCount == 0)
                        {
                            sTable.Append("<tr>\n");
                        }

                        AppendAdvancedCell(sTable, mItem, newMediaFolderName, thumbFolder + "/folder_" + mItem.FolderId + "/", true, mCnt);

                        if (mItem is MediaItemBitmap)
                        {
                            jsonList.Add("{\"id\":" + mItem.Id
                                + ",\"name\":\"" + System.Web.HttpUtility.HtmlEncode(System.IO.Path.GetFileNameWithoutExtension(mItem.FullName)) + "\""
                                + (String.IsNullOrWhiteSpace(mItem.Description) ? String.Empty : ",\"description\":\"" + System.Web.HttpUtility.HtmlEncode(mItem.Description) + "\"")
                                + ",\"href\":\"" + mItem.FileObject.Directory.FullName.Substring(rootFolder.Length + 1) + "/preview/" + mItem.FileObject.Name + "\""
                                + ",\"path\":\"" + thumbFolder + "/folder_" + mItem.FolderId + "/tn_" + System.IO.Path.GetFileNameWithoutExtension(mItem.FullName) + ".jpg\"}");
                        }

                        collCount++;
                        if (collCount >= colls)
                        {
                            sTable.Append("</tr>\n");
                            collCount = 0;
                        }

                        mCnt++;

                        if (this.OnUpdate != null)
                        {
                            this.OnUpdate(this, new MediaItemCallbackArgs(cnt, allMitemsCount, null, false, false));
                        }
                        cnt++;
                    }

                    sTable.Append("<tr><td colspan=\"" + colls + "\">\n");
                    sTable.Append("<a href=\"" + Path.GetFileName(exportPath) + "?show=" + categoryNode.Id + "\" target=\"_blank\">" +
                        "<p style=\"text-align: right; padding:10px;\" class=\"directLink\">" + allLeafsDic[categoryNode].Count + "x " +
                        System.Web.HttpUtility.HtmlEncode(" [" + categoryNode.FullPath.Substring(categoryRootNode.FullPath.Length) + "]".Trim().Trim('\\').Replace("\\", " / ")) + "</p></a>");

                    sTable.Append("<td></tr>\n");
                    sTable.Append("</table>\n");
                }


                File.WriteAllText(rootFolder + "\\" + thumbFolder + "\\cat_" + categoryNode.Id + ".js", "[" + String.Join(",", jsonList) + "]", Encoding.UTF8);
                File.WriteAllText(rootFolder + "\\" + thumbFolder + "\\cat_" + categoryNode.Id + ".tbl", sTable.ToString(), Encoding.UTF8);
            }

            foreach (string file in Directory.GetFiles(resourcesPath + "\\Category"))
            {
                if (!File.Exists(rootFolder +
                        "\\" + thumbFolder + "\\" + Path.GetFileName(file)))
                {
                    File.Copy(file, rootFolder +
                            "\\" + thumbFolder + "\\" + Path.GetFileName(file), false);
                }
            }

            if (!File.Exists(rootFolder + "\\" + thumbFolder + "\\default.css"))
            {
                File.Copy(resourcesPath + "\\default.css", rootFolder +
                        "\\" + thumbFolder + "\\default.css", false);
            }

            if (!File.Exists(rootFolder + "\\" + thumbFolder + "\\video.gif"))
            {
                File.Copy(resourcesPath + "\\video.gif", rootFolder +
                "\\" + thumbFolder + "\\video.gif", false);
            }

            if (!File.Exists(rootFolder + "\\" + thumbFolder + "\\audio.png"))
            {
                File.Copy(resourcesPath + "\\audio.png", rootFolder +
                "\\" + thumbFolder + "\\audio.png", false);
            }

            if (!File.Exists(rootFolder + "\\" + thumbFolder + "\\bg_button_a.gif"))
            {
                File.Copy(resourcesPath + "\\bg_button_a.gif", rootFolder +
                "\\" + thumbFolder + "\\bg_button_a.gif", false);
            }

            if (!File.Exists(rootFolder + "\\" + thumbFolder + "\\bg_button_span.gif"))
            {
                File.Copy(resourcesPath + "\\bg_button_span.gif", rootFolder +
                "\\" + thumbFolder + "\\bg_button_span.gif", false);
            }

            File.WriteAllText(exportPath, sb.ToString(), Encoding.UTF8);
            File.WriteAllText(rootFolder + "\\" + thumbFolder + "\\thumbnails.txt", thumbsSb.ToString(), Encoding.Default);
            File.WriteAllText(rootFolder + "\\" + thumbFolder + "\\previews.txt", previewSb.ToString(), Encoding.Default);
        }

        private static void treeRecursive(StringBuilder sb, Category node, List<Category> allLeafsList)
        {
            foreach (Category child in node.Children)
            {
                if (child.Children.Count == 0)
                {
                    allLeafsList.Add(child);
                    sb.Append("d.add(" + child.Id + "," + node.Id + ",'" + System.Web.HttpUtility.HtmlEncode(child.Name) + "','javascript:ajaxReplace(\\'cat_" + child.Id + ".tbl\\')');\n");
                }
                else
                {
                    sb.Append("d.add(" + child.Id + "," + node.Id + ",'" + System.Web.HttpUtility.HtmlEncode(child.Name) + "');\n");
                }
                treeRecursive(sb, child, allLeafsList);
            }
        }

        private void AppendAdvancedCell(StringBuilder sbHTML, MediaItem mItem, string mediaFolder, string thumbFolder, bool javascriptViewer, int pos)
        {
            string description = "";

            if (limitDescription > 0)
            {
                string htPath = Path.GetDirectoryName(mItem.FileObject.FullName) + "\\"
                    + Path.GetFileNameWithoutExtension(mItem.FileObject.FullName) + ".ht_";

                if (File.Exists(htPath))
                {
                    description = File.ReadAllText(htPath, System.Text.Encoding.UTF8);
                }
                else
                {
                    description = "<p class=\"thumbDescription\">" +
                         description +
                         "</p>\n";
                }
            }

            string videoIcon = "";
            string audioIcon = "";
            string audioFile = "";

            if (displayIcon)
            {
                string extraFile = mItem.FileObject.Directory.FullName + "\\" +
                        Path.GetFileNameWithoutExtension(mItem.FileObject.Name);

                if (File.Exists(extraFile + ".mp3"))
                {
                    audioFile = Path.GetFileNameWithoutExtension(mItem.FileObject.Name) + ".mp3";
                }
                else if (File.Exists(extraFile + ".wav"))
                {
                    audioFile = Path.GetFileNameWithoutExtension(mItem.FileObject.Name) + ".wav";
                }

                if (audioFile.Length != 0)
                {
                    audioIcon = "<img style=\"cursor: hand;\" onClick=\"DHTMLSound('" + mediaFolder + audioFile + "')\" class=\"soundIcon\" src=\"thumbs/audio.png\">";
                }

                if (mItem is MediaItemVideo)
                {
                    videoIcon = "<img class=\"videoIcon\" title=\"Video\" src=\"thumbs/video.gif\">";
                }
            }

            string mediaPath;

            if (javascriptViewer)
            {
                if (mItem is MediaItemBitmap
                    && File.Exists(mItem.FileObject.Directory.FullName + "\\preview\\" + mItem.FileObject.Name))
                {
                    mediaPath = "javascript:showMediaItem(" + mItem.Id + ", " + pos + ", '" + mediaFolder.Trim('/') + "', '" + mItem.FileObject.Name.Trim('/') + "', 'preview' );";
                }
                else
                {
                    mediaPath = "javascript:showMediaItem(" + mItem.Id + ", " + pos + ", '" + mediaFolder.Trim('/') + "', '" + mItem.FileObject.Name.Trim('/') + "', '' );";
                }
            }
            else
            {
                if (mItem is MediaItemBitmap)
                {
                    if (File.Exists(mItem.FileObject.Directory.FullName + "\\preview\\" + mItem.FileObject.Name))
                    {
                        mediaPath = "preview.htm?path=" +
                            System.Web.HttpUtility.UrlEncode(mediaFolder.Trim('/')) + "&name=" +
                            System.Web.HttpUtility.UrlEncode(mItem.FileObject.Name) + "&type=rgb";
                    }
                    else
                    {
                        mediaPath = mediaFolder + mItem.FileObject.Name;
                    }
                }
                else
                {
                    mediaPath = mediaFolder + mItem.FileObject.Name;
                }
            }

            sbHTML.Append("<td class=\"itemCell\" id=\"" + mItem.Id + "\">"
                 + (mItem is MediaItemVideo && displayIcon ? "<div style=\"position: relative; width:100%;\">" : "")
                + "<a name=\"imageLinkHref\" "
                + (javascriptViewer ? "" : "target=\"_blank\"")
                + " href=\"" + mediaPath + "\">\n");

            sbHTML.Append(videoIcon + "<img class=\"itemThumb\" alt=\""
                + mItem.FileObject.Name + "\" title=\""
                + GetToolTip(mItem) + "\" src=\"" + thumbFolder + "tn_"
                + (mItem.FileObject.Name.ToLower().EndsWith(".jpg") ? mItem.FileObject.Name : mItem.FileObject.Name + ".jpg")
                + "\"></a>"
                + (displayFilename ? "<p class=\"thumbTitle\">" + Path.GetFileNameWithoutExtension(mItem.Tag.ToString()).Replace('_', ' ').Trim() + "</p>" : "")
                + description
                + audioIcon
                + (mItem is MediaItemVideo && displayIcon ? "</div>" : "")
                + "</td>\n");
        }

        public static string GetToolTip(MediaItem mItem)
        {
            return GetToolTip(mItem, new Size(mItem.Width, mItem.Height), mItem.FileLength, " / ");
        }

        public static string GetToolTip(MediaItem mItem, Size resizedSize, long fileSize, string delimiter)
        {
            string infoString = "";

            MetaData expTime = mItem.MetaData.FindSoft("Exposure Time");
            MetaData fNumber = mItem.MetaData.FindSoft("F-Number");
            MetaData iso = mItem.MetaData.FindSoft("ISO");
            MetaData make = mItem.MetaData.FindSoft("Make");
            MetaData model = mItem.MetaData.FindSoft("Model");


            if (!model.Null)
            {
                infoString += model.Value;

                if (!make.Null)
                {
                    if (!infoString.Contains(make.Value)
                        && !infoString.ToLower().Contains("nikon")
                        && !infoString.ToLower().Contains("canon"))
                    {
                        infoString = make.Value + " " + infoString;
                    }
                }
            }

            if (infoString.Length > 0)
                infoString += delimiter;

            infoString += mItem.MediaDate.ToString("g");

            infoString += (infoString.Length > 0 ? delimiter : "") + (expTime.Null ? "" : " "
                + (expTime.Value.Contains("s") ? expTime.Value : expTime.Value + "s"))
                + (fNumber.Null ? "" : " " + fNumber.Value)
                + (iso.Null ? "" : " " + (iso.Value.ToLower().Contains("iso") ? iso.Value : "ISO" + iso.Value));

            //"Aperture Value", "ISO Speed Ratings"
            return System.Web.HttpUtility.HtmlEncode(infoString + (infoString.Length > 0 ? delimiter : "")
                                + mItem.Filename + delimiter + resizedSize.Width + "x" + resizedSize.Height +
                                "px" + delimiter + (mItem is MediaItemBitmap ? MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(mItem.Duration) + delimiter : "") +
                                string.Format("{0:0,0}", (fileSize / 1024)) + " KB");
        }


        public void Dispose()
        {
            if (this.takeSnapshot != null)
                this.takeSnapshot.Dispose();
        }
    }
}
