using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class TimeReplacement : ReplacementBase
    {
        public TimeReplacement() { }

        public TimeReplacement(string timeFormat)
        {
            this._timeFormat = timeFormat;
        }

        private string _timeFormat = "t";

        protected override string GetReplacement(RenameFile inputFile)
        {
            string replacement;
            if (string.IsNullOrEmpty(this._timeFormat))
            {
                replacement = DateTime.Now.ToShortTimeString();
            }
            else
            {
                replacement = DateTime.Now.ToString(this._timeFormat);
            }
            return replacement;
        }

        public override void Reset()
        {
            if (this.Arguments.Count == 1)
            {
                this._timeFormat = this.Arguments[0];
            }
            else
            {
                this._timeFormat = "t";
            }
        }

        public override string EscapeKey
        {
            get { return "%time%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt die aktuelle Uhrzeit ein. Eine Formatierung kann übergeben werden: %time%{t}";
            }
        }
    }
}
