namespace GlassTL.Telegram.MTProto
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using Utils;
    using Newtonsoft.Json.Linq;

    public partial class TLObject
    {
        /// <summary>
        /// Reads and returns a TL Serialized int as a JValue
        /// </summary>
        private static JValue DeserializeInt(BinaryReader stream) => new(IntegerUtil.Deserialize(stream));

        /// <summary>
        /// Reads and returns a TL Serialized int128 as a JValue
        /// </summary>
        private static JValue DeserializeInt128(BinaryReader stream) => new(Int128Util.Deserialize(stream));

        /// <summary>
        /// Reads and returns a TL Serialized int256 as a JValue
        /// </summary>
        private static JValue DeserializeInt256(BinaryReader stream) => new(Int256Util.Deserialize(stream));

        /// <summary>
        /// Reads and returns a TL Serialized Bool as a JValue
        /// </summary>
        private static JValue DeserializeBool(BinaryReader stream) => new(BoolUtil.Deserialize(stream));

        /// <summary>
        /// Reads and returns a TL Serialized string as a JValue
        /// </summary>
        private static JValue DeserializeString(BinaryReader stream) => new(StringUtil.Deserialize(stream));
        
        /// <summary>
        /// Reads and returns a TL Serialized byte[] as a JValue
        /// </summary>
        private static JValue DeserializeBytes(BinaryReader stream) => new(BytesUtil.Deserialize(stream));
        
        /// <summary>
        /// Reads and returns a TL Serialized long as a JValue
        /// </summary>
        private static JValue DeserializeLong(BinaryReader stream) => new(LongUtil.Deserialize(stream));

        /// <summary>
        /// Reads and returns a TLObject from a stream
        /// </summary>
        public static TLObject Deserialize(BinaryReader stream) => new(DeserializeObject(stream));

        /// <summary>
        /// Reads and returns a TL Serialized flag as a JToken
        /// </summary>
        private static JToken DeserializeFlag(BinaryReader stream, int flag, string type)
        {
            /*
             * ToDo: An additional (optional) check would be to subtract the
             *  bitwise AND result from the flag param.  If the flag param
             *  is not 0 by the end of parsing, something went wrong.
             */

            // Parse the flag info
            var match = Regex.Match(type, @"flags\.(\d+)\?(.+)");
            if (!match.Success) throw new Exception("Attempted to parse information about a flag in an unknown format.");

            // If the regex passed, this group will always be numeric
            var power = int.Parse(match.Groups[1].Value);

            // If this is "true", it means that the param is a bool encoded into the flag.  We can
            // process here and skip the rest of the code.
            if (match.Groups[2].Value == "true")
            {
                // If the bitwise AND operation is 0, the result is false.  Thus, anything other than 0 is true.
                return new JValue((flag & (int)Math.Pow(2, power)) != 0);
            }

            /*
             * Getting here means that the flag indicates data yet in the stream.
             * We can stop processing if we know there won't be anything in the stream.
             */

            // Again, if the bitwise AND operation is 0, the result is false
            if ((flag & (int)Math.Pow(2, power)) == 0)
            {
                /*
                 * This information is not going to be in the stream.  We could:
                 *  ● Ignore it and not put it in the TLObject
                 *  ● Put it in and make it null
                 *  ● Put it in and make it an empty string
                 *    - This presents problems because nothing other than null is null
                 *
                 * The best choice is probably to make it null and allow users to check themselves
                 */

                return JValue.CreateNull();
            }

            // The param exists in the stream.  Determine how to process
            switch (match.Groups[2].Value)
            {
                case "int":
                    return DeserializeInt(stream);
                case "int128":
                    return DeserializeInt128(stream);
                case "int256":
                    return DeserializeInt256(stream);
                case "Bool":
                    return DeserializeBool(stream);
                case "string":
                    return DeserializeString(stream);
                case "bytes":
                    return DeserializeBytes(stream);
                case "long":
                    return DeserializeLong(stream);
                default:
                    /*
                     * We may be faced with one of the following:
                     *  ● A Vector type, which could be a Vector of anything
                     *  ● A TLObject type
                     */

                    if (match.Groups[2].Value.ToLower().StartsWith("vector"))
                    {
                        return DeserializeVector(stream, match.Groups[2].Value);
                    }

                    // Unknown type in the stream probably means it's another TLObject
                    return DeserializeObject(stream);
            }
        }
        /// <summary>
        /// Reads and returns a TL Serialized Vector as a JArray
        /// </summary>
        private static JArray DeserializeVector(BinaryReader stream, string type)
        {
            // Just because we can't trust the stream...
            if (IntegerUtil.Deserialize(stream) != (int)FindConstructor("Vector")["id"])
            {
                throw new Exception("Attempted to parse a Vector that wasn't a Vector.");
            }

            // Parse the vector info
            var match = Regex.Match(type, "Vector<(.+)>", RegexOptions.IgnoreCase);
            if (!match.Success) throw new Exception("Attempted to parse information about a vector in an unknown format.");

            // Create the object based to hold the vector's items
            var vector = new JArray();
            // Determine how many items are in the stream
            var count = IntegerUtil.Deserialize(stream);

            for (var i = 0; i < count; i++)
            {
                // Deserialize as needed
                switch (match.Groups[1].Value)
                {
                    case "int":
                        vector.Add(DeserializeInt(stream));
                        break;
                    case "int128":
                        vector.Add(DeserializeInt128(stream));
                        break;
                    case "int256":
                        vector.Add(DeserializeInt256(stream));
                        break;
                    case "Bool":
                        vector.Add(DeserializeBool(stream));
                        break;
                    case "string":
                        vector.Add(DeserializeString(stream));
                        break;
                    case "bytes":
                        vector.Add(DeserializeBytes(stream));
                        break;
                    case "long":
                        vector.Add(DeserializeLong(stream));
                        break;
                    default:
                        // Assume it's another TLObject
                        // ToDo: Are we going to support vectors of vectors?
                        vector.Add(DeserializeObject(stream));
                        break;
                }
            }

            return vector;
        }

        /// <summary>
        /// Reads and returns a TL Serialized TLObject as a JToken
        /// </summary>
        private static JToken DeserializeObject(BinaryReader stream)
        {
            Logger.Log(Logger.Level.Info, $"Attempting to deserialize TLObject");

            /*
             * This function assumes that the schema is valid and correct.
             */

            try
            {
                // Read the constructor from the stream
                var constructor = IntegerUtil.Deserialize(stream);

                // Determine if the type should be handled manually
                if (Enum.IsDefined(typeof(ManualTypes.Constructors), (uint)constructor))
                {
                    // Handle manually and return that
                    return ManualTypes.Parse(stream, (ManualTypes.Constructors)constructor);
                }

                // Look up the TLObject's skeleton from the layer schema and fail if not found
                var tlSkeleton = FindConstructor(constructor) ??
                    throw new Exception("Unable to deserialize the TLObject because the constructor cannot be matched with anything in the current layer.");

                // Create the object to return
                var tlObject = JToken.FromObject(new
                {
                    _ = tlSkeleton["name"]
                });

                // In the case that we are deserializing a vector, we cannot loop through the
                // skeleton because the skeleton has no info...
                if (tlSkeleton["name"].ToString().Contains("vector"))
                {
                    // Undo reading the constructor
                    stream.BaseStream.Position -= 4;
                    // Attempt to deserialize as an unknown vector and return that
                    return DeserializeVector(stream, "vector<unknown>");
                }

                // By looping through the params of the skeleton, we can know the order they 
                // will be found in the stream
                foreach (var skeletonParam in tlSkeleton["params"])
                {
                    var name = skeletonParam.Value<string>("name");
                    var type = skeletonParam.Value<string>("type");

                    // Determine how to process this param
                    switch (type)
                    {
                        // Read from the stream as specified by the param type
                        case "#":
                        case "int":
                            tlObject[name] = DeserializeInt(stream);
                            break;
                        case "int128":
                            tlObject[name] = DeserializeInt128(stream);
                            break;
                        case "int256":
                            tlObject[name] = DeserializeInt256(stream);
                            break;
                        case "Bool":
                            tlObject[name] = DeserializeBool(stream);
                            break;
                        case "string":
                            tlObject[name] = DeserializeString(stream);
                            break;
                        case "bytes":
                            tlObject[name] = DeserializeBytes(stream);
                            break;
                        case "long":
                            tlObject[name] = DeserializeLong(stream);
                            break;
                        default:
                            /* 
                             * Here, we may be faced with one of the following:
                             *  ● An optional param based on a flag
                             *  ● A Vector type, which could be a Vector of anything
                             *  ● A TLObject type
                             *  
                             */

                            // Start with flags
                            if (type.StartsWith("flags"))
                            {
                                // Since we are deserializing in the correct order, we assume that we've already read flags correctly
                                tlObject[name] = DeserializeFlag(stream, tlObject.Value<int>("flags"), type);
                                break;
                            }
                            else if (type.ToLower().StartsWith("vector"))
                            {
                                // "Does this need a comment?" ~Bob WeHadABabyItsAVector
                                tlObject[name] = DeserializeVector(stream, (string)skeletonParam["type"]);
                                break;
                            }

                            // Assume this is a TLObject and process accordingly
                            tlObject[name] = DeserializeObject(stream);
                            break;
                    }
                }

                Logger.Log(Logger.Level.Info, $"Successfully deserialized TLObject {tlObject["_"]}");

                return tlObject;
            }
            catch (Exception ex)
            {
                // If an error occured, return null.  There should be checks in place to determine if this happened
                Logger.Log(Logger.Level.Error, $"Unable to deserialize TLObject.\n\n{ex.Message}");
                return null;
            }
        }

        //public static TLObject Deserialize(byte[] data)
        //{
        //    using var memory = new MemoryStream(data);
        //    using var reader = new BinaryReader(memory);
        //
        //    return new TLObject(reader);
        //}
    }
}
