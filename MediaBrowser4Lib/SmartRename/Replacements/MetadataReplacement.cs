using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartRename.Replacements;
using SmartRename;

namespace SmartRename.Replacements
{
    public class MetadataReplacement : ReplacementBase
    {
        public MetadataReplacement() { }

        private string _key;
        private string Key
        {
            get { return _key; }
            set { _key = value; }
        }

        private int _padding;
        private int Padding
        {
            get { return _padding; }
            set { _padding = value; }
        }

        protected override string GetReplacement(RenameFile inputFile)
        {
            string replacement = String.Empty;

            if (this.Key != null)
            {
                replacement = inputFile.MediaItem.MetaData.FirstOrDefault(x => x.Name.Trim() == this.Key).Value;

                if (replacement == null)
                    replacement = inputFile.MediaItem.MetaData.FirstOrDefault(x => x.Name.ToLower().Contains(this.Key.ToLower())).Value;
                
                replacement = (replacement ?? String.Empty);
            }

            return this.Padding > 0 ? replacement.PadLeft(this.Padding, '_') : replacement;
        }

        public override void Reset()
        {

            if (this.Arguments.Count > 0)
            {
                this._key = this.Arguments[0].Trim();
            }
            else
            {
                this._key = null;
            }

            int seed;
            if (this.Arguments.Count == 2 && int.TryParse(this.Arguments[1], out seed))
            {
                this._padding = seed;
            }
            else
            {
                this._padding = 0;
            }
        }

        public override string EscapeKey
        {
            get { return "%metadata%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt einen Wert aus der Metadaten-liste entsprechend dem Schlüssel und definierbarer Länge ein: %metadata%{Schlüssel:Länge}";
            }
        }
    }
}
