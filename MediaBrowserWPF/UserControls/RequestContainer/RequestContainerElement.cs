using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaBrowser4;
using MediaBrowser4.Objects;
using System.ComponentModel;

namespace MediaBrowserWPF.UserControls
{
    public enum RequestContainerElementType { USER_DEFINED, NEWEST_CATEGORY, LAST_CATEGORY, SHUFFLE_CATEGORY, SORT, CATEGORY_NO_DATE, CATEGORY_NO_LOCATION, CATEGORY_NO, CATEGORY_ONLY_DATE, DUBLICATES, FILE_NOT_EXISTS }
    public enum RequestContainerSortType { VIEWED, SHUFFLE }

    public class RequestContainerElement : INotifyPropertyChanged, IEquatable<RequestContainerElement>
    {
        private int elementId;
        private static int idCnt;
        private RequestContainerElement()
        {
        }

        public RequestContainerElement(MediaItemRequest request)
        {
            this.ElementType = RequestContainerElementType.USER_DEFINED;
            this.Request = request;
            this.elementId = request.UserDefinedId;
        }

        public RequestContainerElement(string name)
        {
            if (name == null)
            {
                throw new Exception("Ein Name muss angegeben werden.");
            }

            idCnt--;
            this.elementId = idCnt;
            this.name = name;
            this.Limit = MediaBrowserContext.LimitRequest;
        }

        private MediaItemRequest request;
        public MediaItemRequest Request
        {
            get
            {
                return this.request;
            }

            set
            {
                this.request = value;
                this.DescriptionX = this.Request.Description + "\n" + this.Request.BaseInfo +
                (this.Request.UserDefinedSearchToken != null ? "\n" + this.Request.UserDefinedSearchToken.ToString() : "");
                this.name = this.Request.UserDefinedName != null ? this.Request.UserDefinedName : this.Request.Header;
            }
        }

        private System.Windows.Visibility visibilityTextBlock = System.Windows.Visibility.Visible;
        public System.Windows.Visibility VisibilityTextBlock
        {
            get { return this.visibilityTextBlock; }
            private set
            {
                this.visibilityTextBlock = value;
                this.Notify("VisibilityTextBlock");
            }
        }

        private System.Windows.Visibility visibilityTextBox = System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility VisibilityTextBox
        {
            get { return this.visibilityTextBox; }
            private set
            {
                this.visibilityTextBox = value;
                this.Notify("VisibilityTextBox");
            }
        }

        public bool IsEditable
        {
            set
            {
                if (this.ElementType != RequestContainerElementType.USER_DEFINED)
                    return;

                if (value)
                {
                    this.EditEscape = false;
                    this.Notify("Name");
                    this.VisibilityTextBlock = System.Windows.Visibility.Collapsed;
                    this.VisibilityTextBox = System.Windows.Visibility.Visible;
                }
                else
                {
                    this.VisibilityTextBlock = System.Windows.Visibility.Visible;
                    this.VisibilityTextBox = System.Windows.Visibility.Collapsed;
                }
            }
        }

        public bool EditEscape = false;
        private string name;
        public string Name
        {
            get
            {
                return this.name.Replace("{d}", this.Days.ToString()).Replace("{l}", this.Limit.ToString());
            }

            set
            {
                if (this.ElementType != RequestContainerElementType.USER_DEFINED
                    || this.EditEscape || value.Trim().Length == 0)
                    return;

                this.name = value;
                this.Notify("Name");
                this.request.UserDefinedName = this.name;
                MediaBrowserContext.SaveUserDefinedRequest(this.request);
            }
        }
        public string DescriptionX { get; set; }
        public string Description
        {
            get
            {
                return this.DescriptionX == null ? (string)null : this.DescriptionX.Replace("{d}", this.Days.ToString()).Replace("{l}", this.Limit.ToString());
            }
        }

        public RequestContainerElementType ElementType { get; set; }
        public RequestContainerSortType SortType { get; set; }

        public int Days { get; set; }
        public int Limit { get; set; }

        public override string ToString()
        {
            return this.Name;
        }

        public override int GetHashCode()
        {
            return this.elementId;
        }

        public bool Equals(RequestContainerElement element)
        {
            if (element == null)
            {
                return false;
            }

            return element.elementId.Equals(this.elementId);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RequestContainerElement);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
