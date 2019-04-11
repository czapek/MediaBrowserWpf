using System;
using System.Data;
using System.Data.SQLite;

namespace MediaBrowser4.DB.SQLite
{
    public class SimpleConnection : IConnectionManager
    {   
        private string dbPath;

        public SimpleConnection(string dbPath)
        {     
            this.dbPath = dbPath;                           
        }

        public void Validate()
        {  
            if(dbPath == null)
                throw new UnvalidDBException("The DB file does not exist: " + dbPath);

            System.IO.FileInfo finf = new System.IO.FileInfo(dbPath);
            if (!finf.Exists)
                throw new UnvalidDBException("The DB file does not exist: " + dbPath);

            if (finf.Length > 15)
            {
                //byte[] bytes
                //    = Utilities.Crypto.GetBytesFromFile(new System.IO.FileInfo(dbPath), 15);

                //System.Text.ASCIIEncoding ascii = new System.Text.ASCIIEncoding();
                //if (ascii.GetString(bytes) != "SQLite format 3")
                //{
                //    throw new UnvalidDBException("The DB file is not an valid SQLite file: " + dbPath);
                //}
                //else if (finf.Length <= 1024)
                //{
                //    throw new UnvalidDBException("The DB file contains no objects: " + dbPath);
                //}
            }
            else
            {
                throw new UnvalidDBException("The DB file contains no objects: " + dbPath);
            }
        }

        public System.Data.Common.DbConnection Connection
        {
            get
            {  
                
                SQLiteConnection connection = new SQLiteConnection(
                        @"Data Source=" + dbPath + ";New=True;UTF8Encoding=True;Version=3"
                    );
               
                    connection.Open();               

                return connection;
            }
        }
    }
}
