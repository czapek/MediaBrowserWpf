using System;
using System.Data;
using System.Data.SqlClient;

namespace MediaBrowser4.DB.SqlServer
{
    public class SimpleConnection : IConnectionManager
    {
        private string DbUserName = "ipec_sql";
        private string DbServer = "imps5\\dc";
        private string DbName = "iPEC";
        private string DbPassword = "ipec_sql";
        private int ServerTimeOut = 90;

        public System.Data.Common.DbConnection Connection
        {
            get
            {
                SqlConnection conn = new SqlConnection(this.GetConnectionString());
                conn.Open();
                return conn;
            }
        }

        private string GetConnectionString()
        {
            return String.Format( "workstation id={0};min pool size=0;max pool size=200;packet size=4096;user id=\"{1}\";data source={2};"
                + "TimeOut={3}; persist security info=True;initial catalog=\"{4}\";password={5}",
                new object[] { System.Net.Dns.GetHostName(), this.DbUserName, this.DbServer,
                this.ServerTimeOut, this.DbName, this.DbPassword} );
        }

        private string GetConnectionString( int timeOut )
        {
            return String.Format( "workstation id={0};min pool size=0;max pool size=200;packet size=4096;user id=\"{1}\";data source={2};"
                    + "TimeOut={3}; persist security info=True;initial catalog=\"{4}\";password={5}",
                new object[] { System.Net.Dns.GetHostName(), this.DbUserName, this.DbServer,
                    timeOut, this.DbName, this.DbPassword} );
        }

        public bool TestDBConnection( int timeout )
        {
            if ( String.IsNullOrEmpty( this.DbName )
                || String.IsNullOrEmpty( this.DbPassword )
                || String.IsNullOrEmpty( this.DbServer )
                || String.IsNullOrEmpty( this.DbUserName ) )
            {
                return false;
            }

            SqlConnection conn = new SqlConnection( this.GetConnectionString( timeout ) );

            try
            {
                conn.Open();
                SqlCommand comm = conn.CreateCommand();
                comm.CommandText = "PRINT 'TestDBConnection'";
                comm.ExecuteNonQuery();
            }
            catch (SqlException)
            {
                return false;
            }
            finally
            {
                if ( conn != null && conn.State.Equals( ConnectionState.Open ) )
                {
                    conn.Close();
                }
            }
            return true;
        }
    }
}
