using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRename.Interfaces
{
    public interface IReplacement
    {
        string EscapeKey { get;}
        string HelpText { get; }

        string GetReplacement(RenameFile inputFile, string[] arguments, bool toUpper);
        void Reset();
    }
}
