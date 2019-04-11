using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser4.Objects;
using System.Globalization;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace MediaBrowser4.DB
{
    public class UpdateDb
    {
        public const string DbVersion = "4.10";

        public static void Update()
        {
            int dbVersionMajor = Convert.ToInt32(MediaBrowserContext.GetDBProperty("DBVersion").Split('.')[0]);
            int dbVersionMinor = Convert.ToInt32(MediaBrowserContext.GetDBProperty("DBVersion").Split('.')[1]);

            if (dbVersionMinor <= 9)
            {
                MediaBrowserContext.ExecuteNonQuery(@"CREATE TABLE ATTACHED (
MEDIAFILES_FK INTEGER not null,
ATTACHMENTS_FK INTEGER not null,
PRIMARY KEY (MEDIAFILES_FK, ATTACHMENTS_FK));");

                MediaBrowserContext.ExecuteNonQuery(@"CREATE TABLE ATTACHMENTS (
ID INTEGER not null,
PATH VALUE nvarchar(8000) not null,
PRIMARY KEY (ID));");

                MediaBrowserContext.ExecuteNonQuery(@"CREATE TRIGGER fkd_ATTACHMENTS_DELETE BEFORE DELETE ON ATTACHMENTS
FOR EACH ROW BEGIN
DELETE from ATTACHED WHERE ATTACHMENTS_FK = OLD.ID;
END;");

                MediaBrowserContext.ExecuteNonQuery(@"DROP TRIGGER fkd_MEDIAFILES_DELETE;");

                MediaBrowserContext.ExecuteNonQuery(@"CREATE TRIGGER fkd_MEDIAFILES_DELETE BEFORE DELETE ON MEDIAFILES
FOR EACH ROW BEGIN
DELETE from MEDIAFILES WHERE ID<>OLD.ID AND FOLDERS_FK = OLD.FOLDERS_FK AND LOWER(FILENAME)=LOWER(OLD.FILENAME);
DELETE from VARIATIONS WHERE MEDIAFILES_FK = OLD.ID;
DELETE from EXTRADATAS WHERE MEDIAFILES_FK = OLD.ID;
DELETE from METADATA WHERE MEDIAFILES_FK = OLD.ID;
DELETE from ATTACHED WHERE MEDIAFILES_FK = OLD.ID;
END;");               
            }

            MediaBrowserContext.SetDBProperty("DBVersion", DbVersion);
            MediaBrowserContext.SaveDBProperties();
        }
    }
}
