using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class DateReplacement : ReplacementBase
    {
    
        public DateReplacement() { }
        public DateReplacement(string dateFormat)
        {
            this._dateFormat = dateFormat;
        }

        private string _dateFormat = "d";

        protected override string GetReplacement(RenameFile inputFile)
        {
            string replacement;
            if (string.IsNullOrEmpty(this._dateFormat))
            {
                replacement = DateTime.Now.ToShortDateString();
            }
            else
            {
                replacement = DateTime.Now.ToString(this._dateFormat);
            }
            return replacement;
        }

        public override void Reset()
        {
            if (this.Arguments.Count == 1)
            {
                this._dateFormat = this.Arguments[0];
            }
            else
            {
                this._dateFormat = "d";
            }
        }

        public override string EscapeKey
        {
            get { return "%date%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt das heutige Datum ein. Eine Formatierung kann übergeben werden: %date%{d}";
            }
        }
    }
}
