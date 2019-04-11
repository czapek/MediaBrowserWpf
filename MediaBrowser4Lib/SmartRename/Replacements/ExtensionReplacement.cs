using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class ExtensionReplacement : ReplacementBase
    {
        private bool _toLower = false;
        protected override string GetReplacement(RenameFile inputFile)
        {            
            string result =  Path.GetExtension(inputFile.OriginalName).TrimStart('.');

            return _toLower ? result.ToLower() : result;
        }

        public override void Reset()
        {
            if (this.Arguments.Count == 1)
            {
                _toLower = true;
            }
            else
            {
                _toLower = false;
            }
        }

        public override string EscapeKey
        {
            get { return "%ext%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt die Dateierweiterung ein.";
            }
        }
    }
}
