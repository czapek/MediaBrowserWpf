using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class FlattenReplacement : ReplacementBase
    {
        public FlattenReplacement() { }
        public FlattenReplacement(char separator)
        {
            this._separator = separator;
        }

        private char _separator = '_';

        protected override string GetReplacement(RenameFile inputFile)
        {
            return inputFile.FilePath.Replace(Path.DirectorySeparatorChar, this._separator).Replace(Path.VolumeSeparatorChar, this._separator);
        }

        public override string EscapeKey
        {
            get { return "%flatten%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt den kompletten angepassten Dateipfad ein.";
            }
        }
    }
}
