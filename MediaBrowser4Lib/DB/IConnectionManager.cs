using System;
using System.Data;

namespace MediaBrowser4.DB
{
    /// <summary>
    /// Kapselt eine Verbindung zu einem SQL Server
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// Gibt eine geöffnete SqlConnection zurück.
        /// </summary>
        /// <returns>Eine offene SqlConnection.</returns>
        System.Data.Common.DbConnection Connection { get; }       
    }
}
