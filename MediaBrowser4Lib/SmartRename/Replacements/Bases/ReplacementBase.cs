using System;
using System.Collections.Generic;
using System.Text;

using SmartRename.Interfaces;

namespace SmartRename.Replacements
{
    public abstract class ReplacementBase : IReplacement
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public ReplacementBase() { }

        protected abstract string GetReplacement(RenameFile inputFile);

        #region IReplacement Members

        public virtual string EscapeKey
        {
            get { return "TODO"; }
        }

        public virtual string HelpText
        {
            get { return "TODO"; }
        }

        private List<string> _arguments;
        protected List<string> Arguments
        {
            get
            {
                if (this._arguments == null)
                {
                    this._arguments = new List<string>();
                }
                return _arguments;
            }
            set { _arguments = value; }
        }

        protected virtual string GetReplacement(RenameFile inputFile, bool toUpper)
        {
            string replacement = this.GetReplacement(inputFile);
            return toUpper ? replacement.ToUpper() : replacement;
        }

        public virtual string GetReplacement(RenameFile inputFile, string[] arguments, bool toUpper)
        {
            if (arguments != null)
            {
                this.Arguments.Clear();
                this.Arguments.AddRange(arguments);

                if (!(this is SmartRename.Replacements.IncrementReplacement))
                    this.Reset();
            }
            return this.GetReplacement(inputFile, toUpper);
        }

        public virtual void Reset() { }

        #endregion

    }
}
