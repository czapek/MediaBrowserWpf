using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser4.Utilities
{
    public class DateAndTime
    {
        public static void GetMediaDate(MediaBrowser4.Objects.MediaItem mItem, string mediaDateFormatString)
        {
            mItem.MediaDate = GetExifDateFromItem(mItem);

            if (mItem.MediaDate.Ticks == 0 && mediaDateFormatString.Length <= mItem.FileObject.Name.Length)
            {
                DateTime dateValue;

                if (DateTime.TryParseExact(mItem.FileObject.Name.Substring(0, mediaDateFormatString.Length),
                        mediaDateFormatString, System.Threading.Thread.CurrentThread.CurrentCulture,
                        System.Globalization.DateTimeStyles.None, out dateValue))
                    mItem.MediaDate = dateValue;
                else if (mItem.FileObject.Name.Length >= 26 && DateTime.TryParseExact(mItem.FileObject.Name.Substring(7, 19),
                        "yyyy-MM-dd-HH-mm-ss", System.Threading.Thread.CurrentThread.CurrentCulture,
                        System.Globalization.DateTimeStyles.None, out dateValue))
                    mItem.MediaDate = dateValue;
                else if (mItem.FileObject.Name.Length >= 23 && DateTime.TryParseExact(mItem.FileObject.Name.Substring(4, 15),
                        "yyyyMMdd_HHmmss", System.Threading.Thread.CurrentThread.CurrentCulture,
                        System.Globalization.DateTimeStyles.None, out dateValue))
                    mItem.MediaDate = dateValue;
            }

            if (mItem.MediaDate.Ticks == 0)
                mItem.MediaDate = mItem.FileObject.CreationTime < mItem.FileObject.LastWriteTime
                    ? mItem.FileObject.CreationTime : mItem.FileObject.LastWriteTime;
        }

        public static DateTime GetExifDateFromItem(MediaBrowser4.Objects.MediaItem item)
        {
            System.DateTime dateTime = new DateTime(0);

            try
            {
                if (item.MetaData != null)
                {
                    MediaBrowser4.Objects.MetaData date = item.MetaData.Find("Date/Time Digitized", "MDEX");

                    if (date.Null)
                    {
                        date = item.MetaData.Find("Date/Time", "MDEX");
                    }

                    if (date.Null)
                    {
                        date = item.MetaData.Find("Create Date", "EXFT");
                    }


                    if (!date.Null)
                    {
                        dateTime =
                            new System.DateTime(Convert.ToInt32(date.Value.Substring(0, 4)),        // Year
                                                Convert.ToInt32(date.Value.Substring(5, 2)),            // Month
                                                Convert.ToInt32(date.Value.Substring(8, 2)),            // Day
                                                Convert.ToInt32(date.Value.Substring(11, 2)),            // Hour
                                                Convert.ToInt32(date.Value.Substring(14, 2)),            // Minute
                                                Convert.ToInt32(date.Value.Substring(17, 2))
                                               );
                    }

                    return dateTime;
                }
                else
                {
                    return dateTime;
                }
            }
            catch
            {
                return dateTime;
            }
        }

        public static string FormatVideoTime(long duration)
        {
            TimeSpan ts = new TimeSpan(duration);
            return FormatVideoTime(ts, 1);
        }

        public static string FormatVideoTime(double duration)
        {
            TimeSpan ts = new TimeSpan((long)(duration * 10000000));
            return FormatVideoTime(ts, 1);
        }

        public static string FormatVideoTime(double duration, int precision)
        {
            TimeSpan ts = new TimeSpan((long)(duration * 10000000));
            return FormatVideoTime(ts, precision);
        }

        private static string FormatVideoTime(TimeSpan ts, int precision)
        {
            if (ts.TotalMinutes < 1.0)
            {
                return String.Format("{0:n" + precision + "}s", ts.TotalSeconds);
            }
            else if (ts.TotalHours < 1.0)
            {
                return ts.Minutes + "m " + ts.Seconds + "s";
            }
            else
            {
                return ts.Hours + "h " + ts.Minutes + "m";
            }
        }

        public static string FormatTimespan(TimeSpan ts)
        {
            string result = "";

            if (ts.Days > 0)
            {
                result += Math.Abs(ts.Days) + "d ";
            }

            if (ts.Hours > 0)
            {
                result += Math.Abs(ts.Hours) + "h ";
            }

            if (ts.Minutes > 0)
            {
                result += Math.Abs(ts.Minutes) + "m ";
            }

            if (result.Length == 0)
            {
                result += Math.Abs(ts.Seconds) + "s";
            }

            return result.Trim();
        }

        public static string GetWeekName(MediaBrowser4.Objects.MediaItem startItem)
        {
            DateTime folderDate = startItem.MediaDate;// GetExifDateFromItem(startItem);
            if (folderDate.Ticks > 0)
            {
                System.Globalization.CultureInfo myCI = System.Threading.Thread.CurrentThread.CurrentCulture;
                // CultureInfo myCI = new CultureInfo("de-DE");
                System.Globalization.Calendar myCal = myCI.Calendar;
                System.Globalization.CalendarWeekRule myCWR = myCI.DateTimeFormat.CalendarWeekRule;
                DayOfWeek firstDayOfTheWeek = DayOfWeek.Monday;

                int backToFirstDay = (int)myCal.GetDayOfWeek(folderDate) - 1;
                if (backToFirstDay < 0) backToFirstDay = 6;

                int week = myCal.GetWeekOfYear(folderDate, myCWR, firstDayOfTheWeek);
                DateTime startDate = folderDate.Subtract(new TimeSpan(backToFirstDay, 0, 0, 0));
                string newFolder = String.Format("{0:0#}", week)
                    + "Woche_ab" + startDate.ToString("dd") +
                    startDate.ToString("MMMM");

                return newFolder;
            }
            else
            {
                return null;
            }
        }

        public static string GetDayName(MediaBrowser4.Objects.MediaItem startItem)
        {
            DateTime folderDate = startItem.MediaDate;// GetExifDateFromItem(startItem);
            if (folderDate.Ticks > 0)
            {
                string newFolder = folderDate.ToString("dd") + ". " + folderDate.ToString("MMMM");
                return newFolder;
            }
            else
            {
                return null;
            }
        }

        public static string GetHourName(MediaBrowser4.Objects.MediaItem startItem)
        {
            DateTime folderDate = startItem.MediaDate;// GetExifDateFromItem(startItem);
            if (folderDate.Ticks > 0)            
                return folderDate.ToString("HH") + ":00 " + folderDate.ToString("dd. MMMM");   
            else          
                return null;            
        }


        public static string GetMonthName(MediaBrowser4.Objects.MediaItem startItem)
        {
            DateTime folderDate = startItem.MediaDate;// GetExifDateFromItem(startItem);
            if (folderDate.Ticks > 0)
            {
                string newFolder = folderDate.ToString("yyyy") + "_" + folderDate.ToString("MMMM");
                return newFolder;
            }
            else
            {
                return null;
            }
        }

        public static int GetWeek(DateTime date)
        {
            if (date.Ticks > 0)
            {
                System.Globalization.CultureInfo myCI = System.Threading.Thread.CurrentThread.CurrentCulture;
                // CultureInfo myCI = new CultureInfo("de-DE");
                System.Globalization.Calendar myCal = myCI.Calendar;
                System.Globalization.CalendarWeekRule myCWR = myCI.DateTimeFormat.CalendarWeekRule;
                DayOfWeek firstDayOfTheWeek = DayOfWeek.Monday;

                int backToFirstDay = (int)myCal.GetDayOfWeek(date) - 1;
                if (backToFirstDay < 0) backToFirstDay = 6;

                return myCal.GetWeekOfYear(date, myCWR, firstDayOfTheWeek);
            }
            else
            {
                return -1;
            }
        }

    }
}
