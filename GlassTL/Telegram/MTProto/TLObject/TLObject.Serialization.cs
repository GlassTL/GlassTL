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
        /// Calculates a flag value based on the TLObject provided
        /// </summary>
        /// <returns>The flag value as an int</returns>
        private int CalculateFlag()
        {
            // Make sure there is data to process and that it's of the correct type
            if (TLJson?["_"] == null || TLJson["_"].Type != JTokenType.String)
            {
                throw new Exception("Cannot calculate the flag due to the current TLObject being invalid.");
            }

            // Look up the TLObject's skeleton from the layer schema and fail if not found
            var tlSkeleton = FindConstructor((string)TLJson["_"]) ??
                throw new Exception("Cannot calculate the flag because the TLObject that was passed is not defined by the current schema.");

            // The master flag to be written
            var flag = 0;

            /*
             * Start a loop through all of the params and see if they are supposed to be
             * encoded in the flags so that they can be handled accordingly.
             * 
             */
            
            // Loop through the skeleton in case some flags are left out
            foreach (var f in tlSkeleton["params"])
            {
                // Parse the flag info
                var flagInfo = Regex.Match(f.Value<string>("type"), @"flags\.(\d+)\?(.+)");
                // Skip everything that isn't supposed to be a included
                if (!flagInfo.Success) continue;

                // This is the actual flag value to encode
                var flagValue = false;
                // If the regex passed, this group will always be numeric
                var power = int.Parse(flagInfo.Groups[1].Value);
                // Name of the skeleton's arg
                var name = f.Value<string>("name");

                // If the value wasn't provided, we can assume it's false
                if (TLJson[name] != null && TLJson[name].Type != JTokenType.Null)
                {
                    // Determine how to process
                    switch (flagInfo.Groups[2].Value)
                    {
                        /* 
                         * NOTE: All of these return false if the user passed incorrect data.  They all check to 
                         *  make sure the type is correct except for the case "true" because it's encoded into
                         *  the flag itself.  The others don't care about the value as long as it exists and is
                         *  of the correct type.
                         *  
                         */
                        case "true":
                            // ToDo: Parse the string and int representations of a bool
                            flagValue = TLJson[name].Type == JTokenType.Boolean && (bool)TLJson[name];
                            break;
                        case "int":
                            // ToDo: Parse the string representation of an int
                            // ToDo: Since long values are also considered integers, should we check for max size?
                            flagValue = TLJson[name].Type == JTokenType.Integer;
                            break;
                        case "int128":
                            flagValue = TLJson[name].Type == JTokenType.Bytes;
                            break;
                        case "int256":
                            flagValue = TLJson[name].Type == JTokenType.Bytes;
                            break;
                        case "Bool":
                            // ToDo: Parse the string and int representations of a bool
                            flagValue = TLJson[name].Type == JTokenType.Boolean;
                            break;
                        case "string":
                            // NOTE: We care about zero-length/empty strings.  Don't test for that.
                            flagValue = TLJson[name].Type == JTokenType.String;
                            break;
                        case "bytes":
                            // ToDo: Parse a hex string and numeric representations of a byte array
                            flagValue = TLJson[name].Type == JTokenType.Bytes;
                            break;
                        case "long":
                            /* 
                             * ToDo: Parse a hex string representation of a long
                             * NOTE: Unfortunately, the Long type is lumped in with the Integer
                             * type.  It would be more aptly named "Number" but we work with what
                             * we have.
                             */
                            flagValue = TLJson[name].Type == JTokenType.Integer;
                            break;
                        default:
                            /*
                             * At this point, it could be:
                             *  ● A Vector type, which would be considered an array
                             *  ● A TLObject type
                             */

                            // We wouldn't normally have to check, but sometimes the schema uses different cases
                            if (flagInfo.Groups[2].Value.ToLower().StartsWith("vector"))
                            {
                                // NOTE: We care about empty arrays, too.  Don't test for that.
                                flagValue = TLJson[name].Type == JTokenType.Array;
                                break;
                            }

                            /*
                             * This is a little bit different as well because we are going to check
                             * the type of object contained.
                             */

                            // The value has to be an object and the predicate must be a string
                            if (TLJson[name].Type != JTokenType.Object || TLJson[name]["_"] == null || TLJson[name]["_"].Type != JTokenType.String)
                            {
                                throw new Exception($"The value passed for \"{TLJson[name].Path}\" does not appear to be valid.  It should be a \"{flagInfo.Groups[2].Value}\" type.");
                            }

                            // We need to be able to get the object
                            var returnType = FindConstructor((string)TLJson[name]["_"]) ??
                                throw new Exception($"Cannot find the TLObject for \"{(string)TLJson[name]["_"]}\".  It should be a \"{flagInfo.Groups[2].Value}\" type.  If you updated the schema, make sure to also update the json definitions.");

                            // The base objects must match (case insensitive)
                            flagValue = string.Equals(returnType["return_type"]?.ToString(), flagInfo.Groups[2].Value, StringComparison.CurrentCultureIgnoreCase);
                            break;
                    }
                }

                // Now that we know whether this flag should be true or false, set it accordingly
                flag = flagValue
                    ? flag | (int)Math.Pow(2, power)      // Bitwise OR
                    : flag & ~(int)Math.Pow(2, power);    // Negated (~) Bitwise AND
            }

            // After all that...
            return flag;
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given int
        /// </summary>
        private static void SerializeInt(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as an int is not supported.  Are you missing a param?");
            }

            // Numeric check
            // ToDo: Parse the string representation of an int
            // ToDo: Since long values are also considered integers, should we check for max size?
            if (token.Type != JTokenType.Integer)
            {
                throw new Exception($"An integer was required but \"{token.Type}\" was passed");
            }

            // Passed.  Write to stream
            IntegerUtil.Serialize((int)token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given int128
        /// </summary>
        private static void SerializeInt128(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as an int128 is not supported.  Are you missing a param?");
            }

            // Numeric check
            if (token.Type != JTokenType.Bytes || ((byte[])token).Length != 16)
            {
                throw new Exception($"A byte[16] was required but \"{token.Type}\" was passed");
            }

            // Passed.  Write to stream
            Int128Util.Serialize((byte[])token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given int256
        /// </summary>
        private static void SerializeInt256(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as an int256 is not supported.  Are you missing a param?");
            }

            // Numeric check
            if (token.Type != JTokenType.Bytes || ((byte[])token).Length != 32)
            {
                throw new Exception($"A byte[32] was required but \"{token.Type}\" was passed");
            }

            // Passed.  Write to stream
            Int256Util.Serialize((byte[])token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given Bool
        /// </summary>
        private static void SerializeBool(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as a Boolean is not supported.  Are you missing a param?");
            }

            // Boolean check
            // ToDo: Parse the string and int representations of a bool
            if (token.Type != JTokenType.Boolean)
            {
                throw new Exception($"A Boolean was required but \"{token.Type}\" was passed");
            }

            // Passed.  Write to stream
            BoolUtil.Serialize((bool)token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given string
        /// </summary>
        private static void SerializeString(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as a String is not supported.  Are you missing a param?");
            }

            // String check
            if (token.Type != JTokenType.String)
            {
                throw new Exception($"A String was required but \"{token.Type}\" was passed");
            }

            // Passed.  Write to stream
            StringUtil.Serialize((string)token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given byte[]
        /// 
        /// Confusing, but remember *TL Serialized* which includes padding/length of bytes, etc
        /// </summary>
        private static void SerializeBytes(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as a Byte String is not supported.  Are you missing a param?");
            }

            // Bytes check
            // ToDo: Parse a hex string and numeric representations of a byte array
            if (token.Type != JTokenType.Bytes)
            {
                throw new Exception($"A byte[] was required but \"{token.Type}\" was passed");
            }

            // Passed.  Write to stream
            BytesUtil.Serialize((byte[])token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given long
        /// </summary>
        private static void SerializeLong(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as a Long is not supported.  Are you missing a param?");
            }

            // Long check
            // ToDo: Parse a hex string representation of a long
            if (token.Type != JTokenType.Integer)
            {
                throw new Exception($"A Long was required but \"{token.Type}\" was passed");
            }

            // Passed.  Write to stream
            LongUtil.Serialize((long)token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given TLObject
        /// </summary>
        private static void SerializeObject(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as a TLObject is not supported.  Are you missing a param?");
            }

            // Object check.
            // ToDo: Can we compare against known TLObjects to ensure it's validity?
            if (token.Type != JTokenType.Object || token["_"] == null || token["_"].Type != JTokenType.String)
            {
                throw new Exception($"A TLObject was required but \"{token.Type}\" was passed");
            }

            // Call Serialize() on it's own object
            new TLObject(token).Serialize(stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given Vector
        /// </summary>
        private static void SerializeVector(JToken token, string type, BinaryWriter stream)
        {
            // Null check
            if (token == null)
            {
                throw new Exception($"Serializing 'null' as a Vector is not supported.  Are you missing a param?");
            }

            // Make sure this is a Vector
            if (token.Type != JTokenType.Array)
            {
                throw new Exception($"A Vector (array) was required but \"{token.Type}\" was passed");
            }

            // Make sure we have a vector type
            if (string.IsNullOrEmpty(type))
            {
                throw new Exception($"A Vector (array) type was not specified and we cannot proceed.  Did the layer change and introduce issues?");
            }

            // If we can't determine the Vector type, we won't know how to proceed.
            // NOTE: This assumes that the schema data is formatted correctly
            // ToDo: Are we not going to support the passing of normal values like 'long' or 'string'?
            //   Does it HAVE to include the vector string?
            var vectorInfo = Regex.Match(type.ToLower(), "vector<(.+)>");
            if (!vectorInfo.Success)
            {
                throw new Exception($"A Vector (array) type was not specified and we cannot proceed.  Did the layer change and introduce issues?");
            }

            // Write a Vector constructor to the stream
            BuildTLObject("Vector").Serialize(stream);

            // It's safe to assume this is a JArray
            var tokenArray = token as JArray;

            // Write the number of items
            IntegerUtil.Serialize(tokenArray.Count, stream);

            // Loop through the number of items and process based on the item type
            foreach (var t in tokenArray)
            {
                switch (vectorInfo.Groups[1].Value)
                {
                    case "int":
                        SerializeInt(t, stream);
                        break;
                    case "int128":
                        SerializeInt128(t, stream);
                        break;
                    case "int256":
                        SerializeInt256(t, stream);
                        break;
                    case "Bool":
                        SerializeBool(t, stream);
                        break;
                    case "string":
                        SerializeString(t, stream);
                        break;
                    case "bytes":
                        SerializeBytes(t, stream);
                        break;
                    case "long":
                        SerializeLong(t, stream);
                        break;
                    default:
                        // ASSUME we are working with a TLObject...
                        // ToDo: Is this the best way to do it?  Will we ever have a Vector of Vector?
                        SerializeObject(t, stream);
                        break;
                }
            }
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given flag
        /// </summary>
        private static void SerializeFlag(JToken token, string type, BinaryWriter stream)
        {
            // Skip params that are not provided and optional
            if (token == null || token.Type == JTokenType.Null) return;

            // Parse the flag info
            var flagInfo = Regex.Match(type, @"flags\.(\d+)\?(.+)");
            if (!flagInfo.Success)
            {
                throw new Exception($"A flag type was not specified and we cannot proceed.  Did the layer change and introduce issues?");
            }

            switch (flagInfo.Groups[2].Value)
            {
                case "true":
                    // Do nothing because it's just a flag encoding
                    break;
                case "int":
                    SerializeInt(token, stream);
                    break;
                case "int128":
                    SerializeInt128(token, stream);
                    break;
                case "int256":
                    SerializeInt256(token, stream);
                    break;
                case "Bool":
                    SerializeBool(token, stream);
                    break;
                case "string":
                    SerializeString(token, stream);
                    break;
                case "bytes":
                    SerializeBytes(token, stream);
                    break;
                case "long":
                    SerializeLong(token, stream);
                    break;
                default:
                    /*
                     * At this point, it could be:
                     *  ● A Vector type, which would be considered an array
                     *  ● A TLObject type
                     *  
                     */

                    if (flagInfo.Groups[2].Value.ToLower().StartsWith("vector"))
                    {
                        SerializeVector(token, flagInfo.Groups[2].Value, stream);
                        break;
                    }

                    // ASSUME we are working with a TLObject...
                    SerializeObject(token, stream);
                    break;
            }
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of the current TLObject
        /// </summary>
        public byte[] Serialize()
        {
            using var memory = new MemoryStream();
            using var writer = new BinaryWriter(memory);

            Serialize(writer);
            return memory.ToArray();
        }
        /// <summary>
        /// Writes a TLObject to a stream
        /// </summary>
        /// <param name="stream">Binary stream</param>
        /// <returns>A serialized TLObject</returns>
        public void Serialize(BinaryWriter stream)
        {
            Logger.Log(Logger.Level.Info, "Attempting to Serialize TLObject");

            try
            {
                /*
                 * Serializing must be done in the correct order and since we cannot rely on the user,
                 * (you thought you were so smart) we have to read from the layer schema directly.
                 * 
                 * NOTE: This assumes that the schema is also correct.
                 */

                // Null check
                if (TLJson?["_"] == null || TLJson["_"].Type != JTokenType.String) throw new Exception($"Serializing 'null' as a TLObject is not supported.");

                // Look up the TLObject's skeleton from the layer schema and fail if not found
                var tlSkeleton = FindConstructor((string)TLJson["_"]) ??
                    throw new Exception("Unable to determine which TLObject we are serializing because the constructor cannot be matched with anything in the current layer.");

                // Write the constructor
                IntegerUtil.Serialize(tlSkeleton.Value<int>("id"), stream);


                // By looping through the params of the skeleton, we can know the order they 
                // should be written to the stream
                foreach (var skeletonParam in tlSkeleton["params"])
                {
                    var name = skeletonParam.Value<string>("name");
                    var type = skeletonParam.Value<string>("type");

                    // Determine how to process this param
                    switch (type)
                    {
                        case "#":
                            IntegerUtil.Serialize(CalculateFlag(), stream);
                            break;
                        case "int":
                            SerializeInt(TLJson[name], stream);
                            break;
                        case "int128":
                            SerializeInt128(TLJson[name], stream);
                            break;
                        case "int256":
                            SerializeInt256(TLJson[name], stream);
                            break;
                        case "Bool":
                            SerializeBool(TLJson[name], stream);
                            break;
                        case "string":
                            SerializeString(TLJson[name], stream);
                            break;
                        case "bytes":
                            SerializeBytes(TLJson[name], stream);
                            break;
                        case "long":
                            SerializeLong(TLJson[name], stream);
                            break;
                        case "!X":
                            // This could theoretically be any valid TLObject function
                            // ToDo: Check to make sure it's actually a function?
                            SerializeObject(TLJson[name], stream);
                            break;
                        default:
                            /* 
                             * Here, we may be faced with one of the following:
                             *  ● An optional param based on a flag
                             *  ● A Vector type, which could be a Vector of anything
                             *  ● A TLObject type
                             */

                            if (type.ToLower().StartsWith("vector"))
                            {
                                SerializeVector(TLJson[name], type, stream);
                                break;
                            }
                            else if (type.StartsWith("flags"))
                            {
                                SerializeFlag(TLJson[(string)skeletonParam["name"]], type, stream);
                                break;
                            }

                            // There's nothing else this could be but a TLObject.
                            SerializeObject(TLJson[name], stream);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Unable to serialize the current TLObject.\n\n{ex.Message}");
                throw;
            }

            Logger.Log(Logger.Level.Info, $"Successfully serialized TLObject {TLJson["_"]}");
        }

        public static implicit operator long(TLObject tlObject)
        {
            return (long)tlObject.TLJson;
        }
        public static implicit operator byte[](TLObject tlObject)
        {
            return (byte[])tlObject.TLJson;
        }
        public static implicit operator string(TLObject tlObject)
        {
            return (string)tlObject.TLJson;
        }
        public static implicit operator bool(TLObject tlObject)
        {
            return (bool)tlObject.TLJson;
        }
        public static implicit operator int(TLObject tlObject)
        {
            return (int)tlObject.TLJson;
        }

        public static implicit operator TLObject(JArray v)
        {
            return new TLObject(v);
        }
        public static implicit operator TLObject(JToken v)
        {
            return new TLObject(v);
        }
    }
}
