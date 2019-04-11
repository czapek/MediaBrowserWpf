using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class IncrementReplacement : ReplacementBase
    {

        public IncrementReplacement() {}

        public IncrementReplacement(int defaultSeedValue) 
        {
            this.Arguments.Add(defaultSeedValue.ToString());
            this._index = defaultSeedValue;
        }

        private int _index;
        private int Index
        {
            get { return _index; }
            set { _index = value; }
        }

        private int _padding;
        private int Padding
        {
            get { return _padding; }
            set { _padding = value; }
        }

        protected override string GetReplacement(RenameFile inputFile)
        { 
            string replacement = this.Index.ToString();
            this.Index++;
            return this.Padding > 0 ? replacement.PadLeft(this.Padding, '0') : replacement;
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
