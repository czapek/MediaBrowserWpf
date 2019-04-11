using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class FileReplacement : ReplacementBase
    {
        protected override string GetReplacement(RenameFile inputFile)
        {
            return inputFile.OriginalNameWithoutExtension;
        }

        public override string EscapeKey
        {
            get { return "%filename%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt den alten Dateinamen ein.";
            }
        }
    }
}
