using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser4
{
    public class UnvalidDBException : Exception
    {
        public UnvalidDBException(string message)
            : base(message)
        {
        }
    }
}
