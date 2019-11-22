using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace GlassTL.Telegram.Objects
{
    public class User
    {
        private JToken RawUser { get; } = null;

        public User(JToken RawUser)
        {
            if (RawUser != null && RawUser.Type != JTokenType.Null) this.RawUser = RawUser;
        }

        private T ParseProperty<T>(string property)
        {
            //if (RawUser == null || RawUser[property] == null || RawUser[property].Type == JTokenType.Null) return default;

            try
            {
                return RawUser[property].Value<T>();
            }
            catch
            {
                return default;
            }
        }

        public bool Self => ParseProperty<bool>("self");
        public bool Contact => ParseProperty<bool>("contact");
        public bool MutualContact => ParseProperty<bool>("mutual_contact");


        //public string FirstName
        //{
        //    get
        //    {
        //        if (RawUser == null || RawUser[") return string.Empty;
        //    }
        //}
    }
}
