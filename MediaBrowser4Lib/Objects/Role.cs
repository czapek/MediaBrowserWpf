using System;
using System.Collections.Generic;
using System.Text;

namespace MediaBrowser4.Objects
{
    public class Role
    {      
        public Role(int id, string name, string password, string description)
        {
            this.Name = name;
            this.Id = id;
            this.Password = password;
            this.Description = description;
        }

        public int Id
        {
            get;
            internal set;
        }

        public string Name
        {
            get;
            internal set;
        }

        public string Password
        {
            get;
            internal set;
        }

        public string Description
        {
            get;
            internal set;
        }

        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == this.GetHashCode();
        }

        public override int GetHashCode()
        {
            return this.Id;
        }
    }
}
