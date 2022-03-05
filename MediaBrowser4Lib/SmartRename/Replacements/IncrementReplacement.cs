using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class IncrementReplacement : ReplacementBase
    {

        private int _index;
        private int _padding; 

        protected override string GetReplacement(RenameFile inputFile)
        { 
            string replacement = this._index.ToString();
            this._index++;
            return this._padding > 0 ? replacement.PadLeft(this._padding, '0') : replacement;
        }

        public override void Reset()
        {
            int seed;
            if (this.Arguments.Count > 0 && int.TryParse(this.Arguments[0], out seed))
            {
                this._index = seed;
            }
            else
            {
                this._index = 1;
            }

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
            get { return "%increment%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt eine aufsteigende Nummer mit definierbarem Startwert und definierbarer Länge ein: %increment%{Startwert:Länge}";
            }
        }
    }
}
