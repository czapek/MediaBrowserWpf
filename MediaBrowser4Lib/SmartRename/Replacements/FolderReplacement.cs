using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SmartRename.Replacements
{
    public class FolderReplacement : ReplacementBase
    {
        private int _levelsUp = 1;
        private int LevelsUp
        {
            get { return _levelsUp; }
            set { _levelsUp = value; }
        }

        private int _stoppLevel = 0;
        private int StoppLevel
        {
            get { return _stoppLevel; }
            set { _stoppLevel = value; }
        }

        protected override string GetReplacement(RenameFile inputFile)
        {
            string[] parts = inputFile.FilePath.Replace("\\\\","\\").Split(Path.DirectorySeparatorChar);

            int start = _stoppLevel;
            int stopp = _levelsUp;

            if (stopp < 1)
                stopp = 1;

            if (start < 0)
                start = 0;

            if (start > parts.Length - 1)
                start = parts.Length - 1;

            List<string> newName = new List<string>();

            for (int i = start; i < parts.Length; i++)
            {
                if (parts.Length - i <= stopp)
                    newName.Add(parts[i].Replace(':', '꞉'));
            }
  
            return String.Join("∕", newName);
        }

        public override void Reset()
        {
            int seed;
            if (this.Arguments.Count > 0 && int.TryParse(this.Arguments[0], out seed))
            {
                this._levelsUp = seed;
            }
            else
            {
                this._levelsUp = 1;
            }

            if (this.Arguments.Count == 2 && int.TryParse(this.Arguments[1], out seed))
            {
                this._stoppLevel = seed;
            }
            else
            {
                this._stoppLevel = 0;
            }
        }

        public override string EscapeKey
        {
            get { return "%folder%"; }
        }

        public override string HelpText
        {
            get
            {
                return "Fügt den Namen des übergeordneten Ordners ein : %folder%{Ebenen nach oben: Stopp bei Ebene}";
            }
        }
    }
}
