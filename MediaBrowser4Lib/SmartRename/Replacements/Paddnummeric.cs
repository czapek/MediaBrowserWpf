using SmartRename;
using SmartRename.Replacements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartRename.Replacements
{
    public class Paddnummeric : ReplacementBase
    {
        private int _padding;

        protected override string GetReplacement(RenameFile inputFile)
        {
            string replacement = null;

            string [] onlyNumbers = Regex.Split(inputFile.OriginalNameWithoutExtension.Trim(), "[^0-9]", RegexOptions.Singleline);
            if(onlyNumbers.Length > 0 && onlyNumbers[0].Length > 0)
            {
                replacement = onlyNumbers[0].PadLeft(this._padding, '0');
            }

            return this._padding > 0 && replacement != null ? ReplaceFirstOccurrence(inputFile.OriginalNameWithoutExtension, onlyNumbers[0], replacement) : inputFile.OriginalNameWithoutExtension;
        }

        public static string ReplaceFirstOccurrence(string Source, string Find, string Replace)
        {
            int Place = Source.IndexOf(Find);
            string result = Source.Remove(Place, Find.Length).Insert(Place, Replace);
            return result;
        }

        public override void Reset()
        {
            int seed;
            if (this.Arguments.Count > 0 && int.TryParse(this.Arguments[0], out seed))
            {
                this._padding = seed;
            }
            else
            {
                this._padding = 0;
            }
        }

        public override string HelpText => "fügt führende Nullen an den Dateinamen an, falls dieser mit Zahlen beginnt";

        public override string EscapeKey
        {
            get { return "%paddnummeric%"; }
        }
    }
}
