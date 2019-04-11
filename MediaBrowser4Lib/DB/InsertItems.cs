using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using MediaBrowser4.Objects;

namespace MediaBrowser4.DB
{
    abstract class InsertItems
    {
        private Thread insertThread;
        protected static Queue<MediaItem> insertQueque;
        protected string connectionString;

        internal InsertItems(string connectionString)
        {
            this.connectionString = connectionString;
           // insertThread = new Thread(InsertInDB);    
            insertQueque = new Queue<MediaItem>();
        }

        internal void AbortInsert()
        {

        }

        internal void AddMediaItem(MediaItem mItem)
        {
            insertQueque.Enqueue(mItem);

            if (insertThread == null || insertThread.ThreadState != ThreadState.Running)
            {
                insertThread = new Thread(InsertInDB);
                insertThread.Start();
            }
        }

        protected abstract void InsertInDB();        
    }
}
