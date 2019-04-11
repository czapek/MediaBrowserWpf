using System;
using System.Data;
using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;
using System.Text;

namespace MediaBrowser4.DB.SQLite
{
    public static class DBAdministration
    {
        public static void CreateDB(string dbPath)
        {
            if (File.Exists(dbPath))
            {
                throw new Exception("File already exists: " + dbPath);
            }

            string connectionString = @"Data Source=" + dbPath + ";New=True;UTF8Encoding=True;Version=3";
            SQLiteConnection con = new SQLiteConnection(connectionString);
            con.Open();

            SQLiteCommand cmd = con.CreateCommand();

            cmd.CommandText = "CREATE TABLE DUAL (ID INTEGER)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DUAL(ID) VALUES(1)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE DBPROPERTIES ("
                + "KEY varchar(50) UNIQUE not null, "
                + "VALUE varchar(500) not null)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('DBVersion','" + UpdateDb.DbVersion + "')";
            cmd.ExecuteNonQuery();

            SQLiteParameter dbName = new SQLiteParameter();
            dbName.DbType = System.Data.DbType.String;
            dbName.ParameterName = "@name";
            cmd.Parameters.Add(dbName);

            dbName.Value = System.Environment.MachineName;
            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('HOST',@name)";
            cmd.ExecuteNonQuery();

            dbName.Value = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('USER',@name)";
            cmd.ExecuteNonQuery();

            dbName.Value = System.Guid.NewGuid().ToString("N");
            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('GUID',@name)";
            cmd.ExecuteNonQuery();

            dbName.Value = Path.GetFileName(dbPath);
            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('FILENAME',@name)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('CheckSumMaxLength','52428800')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('CommitInsertSeconds','5')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('DefaultRoleID','1')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('ThumbnailJPEGQuality','80')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('ThumbnailSize','150')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('InitialDBTreePath','')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('InitialCategoryTreePath','')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('DiaryCategorizeFolder','" + MediaBrowserContext.CategoryDiary + "')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('TagFolder','Verschlagwortung')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('MissingFileBehavior','show')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('AutoCategorizeDate','1')";
            cmd.ExecuteNonQuery();

            dbName.Value = Path.GetDirectoryName(dbPath);
            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('DefaultMediaArchivFolder',@name)";
            cmd.ExecuteNonQuery();

            dbName.Value = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            cmd.CommandText = "INSERT INTO DBPROPERTIES(KEY, VALUE) VALUES('DefaultMediaTempFolder',@name)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE ATTACHED (
MEDIAFILES_FK INTEGER not null,
ATTACHMENTS_FK INTEGER not null,
PRIMARY KEY (MEDIAFILES_FK, ATTACHMENTS_FK));";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE ATTACHMENTS (
ID INTEGER not null,
PATH VALUE nvarchar(8000) not null,
PRIMARY KEY (ID));";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TRIGGER fkd_ATTACHMENTS_DELETE BEFORE DELETE ON ATTACHMENTS
FOR EACH ROW BEGIN
DELETE from ATTACHED WHERE ATTACHMENTS_FK = OLD.ID;
END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE MEDIAFILES (ID INTEGER PRIMARY KEY, "
                + "FILENAME varchar(255) not null, "
                + "SORTORDER varchar(255) not null, "
                + "FOLDERS_FK INTEGER not null, "
                + "MD5VALUE varchar(32) not null, "
                + "LENGTH INTEGER, "
                + "VIEWED INTEGER, "
                + "ISDELETED BOOLEAN DEFAULT FALSE not null, "
                + "DELETEDATE TIMESTAMP, "
                + "ISBOOKMARKED BOOLEAN DEFAULT FALSE not null, "
                + "CREATIONDATE TIMESTAMP not null, "
                + "MEDIADATE TIMESTAMP not null, "
                + "EDITDATE TIMESTAMP not null, "
                + "INSERTDATE TIMESTAMP not null, "
                + "ORIENTATION INTEGER, "
                + "TYPE char(3), "
                + "PRIVACY INTEGER, "
                + "CURRENTVARIATION INTEGER, "
                + "DESCRIPTION_FK INTEGER null, "
                + "DURATION FLOAT, "
                + "FRAMES INTEGER, "
                + "PRIORITY INTEGER, "
                + "ROLES_FK INTEGER NOT NULL, "
                + "HISTORYVERSION INTEGER DEFAULT 0 NOT NULL, "
                + "ISDUBLICATE BOOLEAN DEFAULT FALSE not null, "
                + "WIDTH INTEGER, "
                + "HEIGHT INTEGER)";

            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_mediafiles_FILENAME on MEDIAFILES (FILENAME, FOLDERS_FK)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_mediafiles_FOLDERS on MEDIAFILES (FOLDERS_FK)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_mediafiles_MD5VALUE on MEDIAFILES (MD5VALUE)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_mediafiles_CURRENTVARIATION on MEDIAFILES (CURRENTVARIATION)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_MEDIAFILES_ISDELETED on MEDIAFILES (ISDELETED)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX [idx_MediafIles_MediaDate] ON [MEDIAFILES]([MEDIADATE]  ASC)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX [idx_MediafIles_DESCRIPTION_FK] ON [MEDIAFILES] (DESCRIPTION_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE VARIATIONS (ID INTEGER PRIMARY KEY, "
                + "MEDIAFILES_FK INTEGER not null, "
                + "POSITION INTEGER DEFAULT 1 NOT NULL, "
                + "NAME varchar(50)"
                + (MediaBrowser4.MediaBrowserContext.ThumbnailSize > 0 ? "" : "THUMB BLOB")
                + ")";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_variations_MEDIAFILES on VARIATIONS (MEDIAFILES_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_MEDIAFILES_DELETE BEFORE DELETE ON MEDIAFILES " +
                         "FOR EACH ROW BEGIN " +
                         "DELETE from MEDIAFILES WHERE ID<>OLD.ID AND FOLDERS_FK = OLD.FOLDERS_FK AND LOWER(FILENAME)=LOWER(OLD.FILENAME); " +
                         "DELETE from VARIATIONS WHERE MEDIAFILES_FK = OLD.ID; " +
                         "DELETE from EXTRADATAS WHERE MEDIAFILES_FK = OLD.ID; " +
                         "DELETE from METADATA WHERE MEDIAFILES_FK = OLD.ID; " +
                         "DELETE from ATTACHED WHERE MEDIAFILES_FK = OLD.ID; " +
                         "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_MEDIAFILES_UPDATE BEFORE UPDATE ON MEDIAFILES " +
                        "FOR EACH ROW BEGIN " +
                        "UPDATE MEDIAFILES SET FILENAME=NEW.FILENAME, SORTORDER=NEW.SORTORDER, FOLDERS_FK=NEW.FOLDERS_FK WHERE ID<>OLD.ID AND FOLDERS_FK = OLD.FOLDERS_FK AND LOWER(FILENAME)=LOWER(OLD.FILENAME); " +
                        "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE DESCRIPTION (ID INTEGER PRIMARY KEY, "
                         + "VALUE nvarchar(8000))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_DESCRIPTION_DELETE BEFORE DELETE ON DESCRIPTION " +
             "FOR EACH ROW BEGIN " +
             "UPDATE MEDIAFILES SET DESCRIPTION_FK=NULL WHERE DESCRIPTION_FK = OLD.ID; " +
             "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX [idx_DESCRIPTION_VALUE] ON [DESCRIPTION] (VALUE);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE LAYERS ("
                + "VARIATIONS_FK INTEGER not null, "
                + "POSITION INTEGER not null, "
                + "EDIT char(4), "
                + "ACTION varchar(512))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_layers_VARIATIONS on LAYERS (VARIATIONS_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_VARIATIONS_DELETE BEFORE DELETE ON VARIATIONS " +
                         "FOR EACH ROW BEGIN " +
                         "DELETE from LAYERS WHERE VARIATIONS_FK = OLD.ID; " +
                         "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE FOLDERS (ID INTEGER PRIMARY KEY, "
                + "FOLDERNAME varchar(500) UNIQUE not null)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_FOLDERS_folder on FOLDERS (foldername)";
            cmd.ExecuteNonQuery(); 

            cmd.CommandText = "CREATE TRIGGER fkd_FOLDERS_DELETE BEFORE DELETE ON FOLDERS " +
                         "FOR EACH ROW BEGIN " +
                         "DELETE from MEDIAFILES WHERE FOLDERS_FK = OLD.ID; " +
                         "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE ROLES (ID INTEGER PRIMARY KEY, "
                + "ROLE char(30) UNIQUE not null, "
                + "PASSWORD char(32),"
                + "DESCRIPTION varchar(500))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_ROLES_folder on ROLES (ROLE)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO ROLES(ROLE) VALUES('PUBLIC')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO ROLES(ROLE) VALUES('PRIVATE')";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE FAVORITES ("
                + "NAME varchar(255) not null, "
                + "TYPE char(3) not null, "
                + "ACCESS TIMESTAMP not null)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE CATEGORY (ID INTEGER PRIMARY KEY, "
                + "PARENT INTEGER, NAME varchar(50) not null, SORTORDER varchar(50) not null,"
                + "DESCRIPTION nvarchar(255), "
                + "ISUNIQUE BOOLEAN DEFAULT FALSE NOT NULL, "
                + "ISLOCATION BOOLEAN DEFAULT FALSE NOT NULL, "
                + "ISDATE BOOLEAN DEFAULT FALSE NOT NULL, "
                + "LATITUDE NUMERIC NULL, LONGITUDE NUMERIC NULL, "
                + "DATE TIMESTAMP NULL, "
                + "FULLPATH nvarchar(8000) NULL, "
                + "GUID char(32) UNIQUE not null)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX [idx_CATEGORY_FULLPATH] ON [CATEGORY] (FULLPATH);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_CATEGORY_PARENT on CATEGORY (parent)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_CATEGORY_NAME on CATEGORY (name)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_CATEGORY_GUID on CATEGORY (GUID)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO CATEGORY(NAME, FULLPATH, SORTORDER, DESCRIPTION, GUID, DATE, ISUNIQUE, ISDATE, ISLOCATION, LATITUDE, LONGITUDE, PARENT) "
            + "VALUES('" + MediaBrowserContext.CategoryDiary + "','Tagebuch','00002','Für jeden Tag wird eine Kategorie angelegt','" + System.Guid.NewGuid().ToString("N") + "', CURRENT_TIMESTAMP, 1, 1, 0, 0, 0, 0)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO CATEGORY(NAME, FULLPATH, SORTORDER, DESCRIPTION, GUID, DATE, ISUNIQUE, ISDATE, ISLOCATION, LATITUDE, LONGITUDE, PARENT) "
             + "VALUES('Orte','Orte','00001','Kategorien die geographisch zuordenbar sind','" + System.Guid.NewGuid().ToString("N") + "', CURRENT_TIMESTAMP, 1, 0, 1, 0, 0, 0)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO CATEGORY(NAME, FULLPATH, SORTORDER, DESCRIPTION, GUID, DATE, ISUNIQUE, ISDATE, ISLOCATION, LATITUDE, LONGITUDE, PARENT) "
          + "VALUES('Verschlagwortung','Verschlagwortung','00000','Über die schnelle Verschlagwortung angelegte Kategorien','" + System.Guid.NewGuid().ToString("N") + "', CURRENT_TIMESTAMP, 0, 0, 0, 0, 0, 0)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE CATEGORIZE ( "
                + "CATEGORY_FK INTEGER not null, "
                + "VARIATIONS_FK INTEGER not null, "
                + "PRIMARY KEY (CATEGORY_FK, VARIATIONS_FK))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_CATEGORIZE_VARIATIONREFERENCE on CATEGORIZE (VARIATIONS_FK)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_CATEGORIZE_CATEGORY on CATEGORIZE (CATEGORY_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_CATEGORIZE_DELETE BEFORE DELETE ON CATEGORY " +
                        "FOR EACH ROW BEGIN " +
                        "DELETE from CATEGORIZE WHERE CATEGORY_FK = OLD.ID; " +
                        "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_CATEGORIZE_VARIATIONS_DELETE BEFORE DELETE ON VARIATIONS " +
                         "FOR EACH ROW BEGIN " +
                         "DELETE from CATEGORIZE WHERE VARIATIONS_FK = OLD.ID; " +
                         "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE REMOTEMAPPING ("
                + "DBGUID char(32) not null, "
                + "OBJECTGUID_LOCAL char(32) not null, "
                + "OBJECTGUID_REMOTE char(32) not null, "
                + "TYPE char(3) not null)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_remotemapping_DBGUID on REMOTEMAPPING (DBGUID)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_remotemapping_OBJECTGUID_LOCAL on REMOTEMAPPING (OBJECTGUID_LOCAL)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_remotemapping_OBJECTGUID_REMOTE on REMOTEMAPPING (OBJECTGUID_REMOTE)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE METADATA (MEDIAFILES_FK INTEGER not null, "
                + "METANAME_FK INTEGER not null, "
                + "VALUE varchar(255) )";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_METADATA_MEDIAFILES_FK on METADATA (MEDIAFILES_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_METADATA_METANAME_FK on METADATA (METANAME_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE METADATANAME (ID INTEGER PRIMARY KEY, "
                + "NAME varchar(50) not null, GROUPNAME varchar(50) not null, TYPE char(4) not null, isVisible BOOLEAN DEFAULT TRUE)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_METADATANAME_NAME on METADATANAME (NAME)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_METADATANAME_DELETE BEFORE DELETE ON METADATANAME " +
                        "FOR EACH ROW BEGIN " +
                        "DELETE from METADATA WHERE METANAME_FK = OLD.ID; " +
                        "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE EXTRADATA (ID INTEGER PRIMARY KEY, "
                + "NAME varchar(255) NOT NULL, "
                + "TYP varchar(3) NOT NULL, "
                + "FOLDER VARCHAR(50), "
                + "CATEGORY VARCHAR(50) DEFAULT 'Default' NOT NULL, "
                + "DATA BLOB )";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE EXTRADATAS ( "
                + "EXTRADATA_FK INTEGER not null, "
                + "MEDIAFILES_FK INTEGER not null, "
                + "PRIMARY KEY (EXTRADATA_FK, MEDIAFILES_FK))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_EXTRADATAS_EXTRADATA on EXTRADATAS (EXTRADATA_FK)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_EXTRADATAS_MEDIAREFERENCE on EXTRADATAS (MEDIAFILES_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_EXTRADATA_DELETE BEFORE DELETE ON EXTRADATA " +
                        "FOR EACH ROW BEGIN " +
                        "DELETE from EXTRADATAS WHERE EXTRADATA_FK = OLD.ID; " +
                        "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_EXTRADATAS_MEDIAFILES_DELETE BEFORE DELETE ON MEDIAFILES " +
                         "FOR EACH ROW BEGIN " +
                         "DELETE from EXTRADATAS WHERE MEDIAFILES_FK = OLD.ID; " +
                         "END;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE USERDEFINEDREQUESTS (ID INTEGER PRIMARY KEY, REQUEST BLOB NOT NULL)";
            cmd.ExecuteNonQuery();


            cmd.CommandText = @"CREATE TABLE DATALOGGER_METEOROLOGY ( 
              LOGTIME TIMESTAMP NOT NULL, 
              TEMPERATURE NUMERIC NOT NULL,  
              HUMIDITY NUMERIC NOT NULL,
              PRIMARY KEY (LOGTIME))";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE DATALOGGER_GPS (
                FILETIME TIMESTAMP NOT NULL, 
                FILESIZE INTEGER NOT NULL,
                UTCTIME TIMESTAMP NOT NULL, 
                LOCALTIME TIMESTAMP NOT NULL, 
                LONGITUDE NUMERIC NOT NULL,  
                LATITUDE NUMERIC NOT NULL,  
                ALTITUDE NUMERIC NULL)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_DATALOGGER_GPS_LOGTIME on DATALOGGER_GPS (LOCALTIME)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_DATALOGGER_GPS_FILETIME on DATALOGGER_GPS (FILETIME)";
            cmd.ExecuteNonQuery();

            cmd.Dispose();
            con.Close();

            CreateThumbnailDB(dbPath, MediaBrowser4.MediaBrowserContext.ThumbnailSize, true);
        }

        public static void CreateThumbnailDB(string dbPath, int size, bool overwrite)
        {
            if (MediaBrowser4.MediaBrowserContext.ThumbnailSize > 0)
            {
                dbPath = GetThumbNailDBPath(dbPath, size);
            }

            CreateThumbnailDB(dbPath, overwrite);
        }

        public static void CreateThumbnailDB(string dbPath, System.Drawing.Size size, bool overwrite)
        {
            if (MediaBrowser4.MediaBrowserContext.ThumbnailSize > 0)
            {
                dbPath = GetThumbNailDBPath(dbPath, size);
            }

            CreateThumbnailDB(dbPath, overwrite);
        }

        public static void CreateThumbnailDB(string dbPath, bool overwrite)
        {
            if (File.Exists(dbPath))
            {
                if (overwrite)
                {
                    File.Delete(dbPath);
                }
                else
                {
                    return;
                }
            }

            string connectionString = @"Data Source=" + dbPath + ";New=" + (overwrite ? "True" : "False") + ";UTF8Encoding=True;Version=3";
            SQLiteConnection con = new SQLiteConnection(connectionString);
            con.Open();

            SQLiteCommand cmd = con.CreateCommand();

            cmd.CommandText = "CREATE TABLE THUMBS (VARIATIONS_FK INTEGER UNIQUE NOT NULL, ISOUTDATED BOOLEAN NOT NULL DEFAULT FALSE, THUMB BLOB NOT NULL, Extension varchar(10) NOT NULL DEFAULT 'jpg', WIDTH INTEGER NULL, HEIGHT INTEGER NULL)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_thumbs_VARIATIONS on THUMBS (VARIATIONS_FK)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TABLE FACES (VARIATIONS_FK INTEGER NOT NULL, "
                + "X INTEGER not null, "
                + "Y INTEGER not null, "
                + "WIDTH INTEGER not null, "
                + "HEIGHT INTEGER not null, "
                + "REL_SIZE FLOAT not null)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE INDEX idx_faces_VARIATIONS on FACES (VARIATIONS_FK)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE INDEX idx_faces_REL_SIZE on FACES (REL_SIZE)";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "CREATE TRIGGER fkd_THUMBS_DELETE BEFORE DELETE ON THUMBS " +
                         "FOR EACH ROW BEGIN " +
                         "DELETE from FACES WHERE VARIATIONS_FK = OLD.VARIATIONS_FK; " +
                         "END;";
            cmd.ExecuteNonQuery();

            cmd.Dispose();
            con.Close();
        }

        internal static string GetThumbNailDBPath(string dbPath)
        {
            return GetThumbNailDBPath(dbPath, MediaBrowser4.MediaBrowserContext.ThumbnailSize);
        }

        internal static string GetThumbNailDBPath(string dbPath, int size)
        {
            return Path.GetDirectoryName(dbPath) + (Path.GetDirectoryName(dbPath).Length > 0 ? "\\" : "")
                     + Path.GetFileNameWithoutExtension(dbPath) +
                     "." + size + Path.GetExtension(dbPath) + "t";
        }

        internal static string GetThumbNailDBPath(string dbPath, System.Drawing.Size size)
        {
            return Path.GetDirectoryName(dbPath) + (Path.GetDirectoryName(dbPath).Length > 0 ? "\\" : "")
                     + Path.GetFileNameWithoutExtension(dbPath) +
                     "." + size.Width + "x" + size.Height + Path.GetExtension(dbPath) + "t";
        }
    }
}
