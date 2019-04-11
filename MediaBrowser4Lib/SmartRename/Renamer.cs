using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

using Rep = SmartRename.Replacements;
using SmartRename.Interfaces;
using System.Text.RegularExpressions;
using MediaBrowser4.Objects;

namespace SmartRename
{
    public class Renamer : ISmartRename
    {
        private string _replaceIllegal;
        public string ReplaceIllegal
        {
            get { return _replaceIllegal; }
            set { _replaceIllegal = value; }
        }

        private string _renameFormat;
        public string RenameFormat
        {
            get { return _renameFormat; }
            set { _renameFormat = value; }
        }

        private List<RenameFile> _renameFiles;
        public List<RenameFile> RenameFiles
        {
            get { return _renameFiles; }
            set { _renameFiles = value; }
        }

        public Renamer() { }

        public static Renamer Factory(List<MediaItem> mediaList)
        {
            Renamer renamer = new Renamer();
            renamer.RenameFiles = new List<RenameFile>();

            foreach (MediaItem mItem in mediaList)
            {
                RenameFile renameFile = new RenameFile(mItem) { ExtraFileVisibility = System.Windows.Visibility.Visible };
                renamer.RenameFiles.Add(renameFile);

                foreach (Attachment attachment in MediaBrowser4.Utilities.FilesAndFolders.GetAttachments(mItem))
                {
                    renamer.RenameFiles.Add(new RenameFile(mItem, attachment.FullName)
                    {
                        ExtraFileVisibility = System.Windows.Visibility.Hidden,
                        MediaItemRenameFile = renameFile
                    });
                }
            }

            return renamer;
        }

        private static string DoReplacement(RenameFile file, string newName, KeyValuePair<string, IReplacement> replacement, bool toUpper)
        {
            if (file.MediaItemRenameFile == null)
            {
                string escapeKey = toUpper ? replacement.Key.ToUpper() : replacement.Key;
                Regex key = new Regex(string.Format("{0}{1}|{0}", escapeKey, "{([ /0-9a-zA-Z:.-]*)}"));
                MatchCollection matches = key.Matches(newName);
                foreach (Match match in matches)
                {
                    string[] arguments = null;
                    if (match.Groups.Count == 2)
                    {
                        string argument = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(argument))
                        {
                            arguments = argument.Split(':');
                        }
                    }

                    newName = newName.Replace(match.Value, replacement.Value.GetReplacement(file, arguments, toUpper));
                }

                return newName;
            }
            else
            {
                return Path.GetFileNameWithoutExtension(file.MediaItemRenameFile.NewName) + Path.GetExtension(file.OriginalName);
            }
        }

        public virtual void SetNewNames()
        {
            //if we have a valid format, update all the "new name" values":
            if (!string.IsNullOrEmpty(this.RenameFormat))
            {
                foreach (RenameFile file in this.RenameFiles)
                {
                    //auslesen der argumente
                    file.RenameResult = Result.NONE;
                    file.RenameResultMessage = string.Empty;
                    this.GetNewName(file);
                    break;
                }

                foreach (IReplacement replacement in this.Replacements.Values)
                {
                    replacement.Reset();
                }

                foreach (RenameFile file in this.RenameFiles)
                {
                    file.NewName = this.GetNewName(file);

                    if (this.ReplaceIllegal != null)
                    {
                        foreach (char illegal in Path.GetInvalidFileNameChars())
                        {
                            file.NewName = file.NewName.Replace(illegal.ToString(), this.ReplaceIllegal);
                        }

                        file.NewName = Path.GetFileNameWithoutExtension(file.NewName).Replace(".", this.ReplaceIllegal) + Path.GetExtension(file.NewName);
                    }
                    else
                    {
                        file.NewName = file.NewName.Replace("\"", "''");
                        file.NewName = file.NewName.Replace(':', '꞉');
                        file.NewName = file.NewName.Replace('/', '∕');
                        file.NewName = file.NewName.Replace('\\', '∕');
                    }
                }
            }
        }

        private string GetNewName(RenameFile file)
        {
            string newName = this.RenameFormat;
            foreach (KeyValuePair<string, IReplacement> replacement in this.Replacements)
            {
                newName = DoReplacement(file, newName, replacement, false);
                newName = DoReplacement(file, newName, replacement, true);
            }
            return newName;
        }

        public List<IReplacement> ReplacementList
        {
            get
            {
                return this.Replacements.Values.ToList();
            }
        }

        private Dictionary<string, IReplacement> _replacements;
        public Dictionary<string, IReplacement> Replacements
        {
            get
            {
                if (this._replacements == null)
                {
                    this._replacements = new Dictionary<string, IReplacement>();
                    Rep.DateReplacement replacement1 = new Rep.DateReplacement();
                    Rep.ExtensionReplacement replacement2 = new Rep.ExtensionReplacement();
                    Rep.FileReplacement replacement3 = new Rep.FileReplacement();
                    Rep.FlattenReplacement replacement4 = new Rep.FlattenReplacement();
                    Rep.FolderReplacement replacement5 = new Rep.FolderReplacement();
                    Rep.IncrementReplacement replacement6 = new Rep.IncrementReplacement();
                    Rep.ReplaceReplacement replacement7 = new Rep.ReplaceReplacement();
                    Rep.TimeReplacement replacement8 = new Rep.TimeReplacement();
                    Rep.MediaDateReplacement replacement9 = new Rep.MediaDateReplacement();
                    Rep.MetadataReplacement replacement10 = new Rep.MetadataReplacement();

                    this._replacements.Add(replacement1.EscapeKey, replacement1);
                    this._replacements.Add(replacement2.EscapeKey, replacement2);
                    this._replacements.Add(replacement3.EscapeKey, replacement3);
                    this._replacements.Add(replacement4.EscapeKey, replacement4);
                    this._replacements.Add(replacement5.EscapeKey, replacement5);
                    this._replacements.Add(replacement6.EscapeKey, replacement6);
                    this._replacements.Add(replacement7.EscapeKey, replacement7);
                    this._replacements.Add(replacement8.EscapeKey, replacement8);
                    this._replacements.Add(replacement9.EscapeKey, replacement9);
                    this._replacements.Add(replacement10.EscapeKey, replacement10);
                }

                return this._replacements;
            }
        }

        public virtual void CommitNewNames()
        {
            foreach (RenameFile file in this.RenameFiles)
            {
                if (file.Rename || (file.MediaItemRenameFile != null && file.MediaItemRenameFile.Rename))
                {
                    file.TryRename();
                }
            }
        }
    }
}
