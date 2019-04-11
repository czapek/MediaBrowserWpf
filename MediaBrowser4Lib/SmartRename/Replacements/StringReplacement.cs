using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace SmartRename.Replacements
{
    public class ReplaceReplacement : ReplacementBase
    {     

        protected override string GetReplacement(RenameFile inputFile)
        {
            string replacement = inputFile.OriginalNameWithoutExtension;
            if (this.Arguments.Count == 2)
            {
               // replacement = Regex.Replace(this.Arguments[0], this.Arguments[1], "", RegexOptions.IgnoreCase);
                //replacement = Regex.Replace("%" + this.Arguments[0] + "%", "%" + this.Arguments[1] + "%", @"$$0", RegexOptions.IgnoreCase);
                replacement = inputFile.OriginalNameWithoutExtension.Replace(this.Arguments[0], this.Arguments[1]);
            }
            return replacement;
        }

        public override string EscapeKey
        {
            get { return "%replace%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Ersetzt ein Schlüsselwort mit einem anderen: %replace%{alt:neu}";
            }
        }
    }
}
