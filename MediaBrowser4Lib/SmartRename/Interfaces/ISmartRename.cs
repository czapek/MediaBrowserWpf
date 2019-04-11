using System;
using System.Collections.Generic;

namespace SmartRename.Interfaces
{
    public interface ISmartRename
    {
        void CommitNewNames();
        List<RenameFile> RenameFiles { get; set; }
        string RenameFormat { get; set; }
        void SetNewNames();
    }
}
