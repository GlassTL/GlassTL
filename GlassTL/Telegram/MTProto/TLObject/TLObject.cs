namespace GlassTL.Telegram.MTProto
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.IO;
    using System.Reflection;

    // ToDo: Add checks when reading from streams to ensure that there are enough bytes to read.
    public partial class TLObject
    {
        private JToken TLJson { get; set; }

        /// <summary>
        /// Reads a serialized <see cref="TLObject" /> from a <see cref="BinaryReader"/>
        /// </summary>
        /// <param name="stream">Stream containing the serialized TLObject</param>
        public TLObject(BinaryReader stream)
        {
            TLJson = DeserializeObject(stream);
        }

        /// <summary>
        /// Loads a TLObject from a JSON object
        /// </summary>
        /// <param name="tlJson"></param>
        public TLObject(JToken tlJson)
        {
            // ToDo: Add some verification?
            TLJson = tlJson;
        }

        /// <summary>
        /// Returns a <see cref="JToken"/> version of this object.
        /// </summary>
        private JToken AsJToken() => TLJson;
        /// <summary>
        /// Allows you to assign a TLObject to a JToken:
        /// 
        /// TLObject foo = new TLObject(...);
        /// JToken bar = foo;
        /// 
        /// </summary>
        public static implicit operator JToken(TLObject o) => o.AsJToken();

        /// <summary>
        /// Gets or sets a child node in the TLObject by key
        /// </summary>
        /// <param name="key">The key of the child node</param>
        public TLObject this[string key]
        {
            get
            {
                if (TLJson == null || TLJson.Type == JTokenType.Null) return null;
                return new TLObject(TLJson[key]);
            }
            set
            {
                if (TLJson == null)
                {
                    TLJson = JObject.FromObject(new { key, value });
                }
                else
                {
                    TLJson[key] = value;
                }
            }
        }
        public T GetAs<T>(string key)
        {
            return TLJson == null ? default : TLJson[key].ToObject<T>();
        }
        /// <summary>
        /// Gets or sets a child node in the TLObject by index
        /// </summary>
        /// <param name="index">The index of the child node</param>
        public TLObject this[int index]
        {
            get => TLJson == null ? null : new TLObject(TLJson[index]);
            set
            {
                if (TLJson == null)
                {
                    TLJson = JObject.FromObject(new { TLJson, value });
                }
                else
                {
                    TLJson[index] = value;
                }
            }
        }
        public JTokenType InternalType => TLJson?.Type ?? JTokenType.Null;
        public override string ToString() => TLJson.ToString();

        private class InternalConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteToken(((TLObject)value).AsJToken().CreateReader());
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof(TLObject).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

    }
}