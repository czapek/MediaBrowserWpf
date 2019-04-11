using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SmartRename.Replacements;
using SmartRename;
using MediaBrowser4;

namespace SmartRename.Replacements
{
    public class MediaDateReplacement : ReplacementBase
    {
        private String _format = MediaBrowserContext.MediaDateDefaultFormatString;

        public MediaDateReplacement() {}

        public MediaDateReplacement(string defaultFormat) 
        {
            this.Arguments.Add(defaultFormat);
            this._format = defaultFormat;
        }

        protected override string GetReplacement(RenameFile inputFile)
        {
            string replacement = inputFile.MediaItem.MediaDate.ToString(_format);

            return replacement;
        }

        public override void Reset()
        {         
            if (this.Arguments.Count == 1)
            {
                this._format = this.Arguments[0];
            }
            else
            {
                this._format = MediaBrowserContext.MediaDateDefaultFormatString;
            }           
        }

        public override string EscapeKey
        {
            get { return "%mediadate%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Verwendet das Erstelldatum (EXIF) des Mediums als Dateiname. Eine Formatierung kann übergeben werden: %mediadate%{yyMMdd-HHmm-ss}";
            }
        }
    }
}
