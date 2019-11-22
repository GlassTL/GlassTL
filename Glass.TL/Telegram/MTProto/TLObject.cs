using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GlassTL.Telegram.Utils;

namespace GlassTL.Telegram.MTProto
{
    // ToDo: Add checks when reading from streams to ensure that there are enough bytes to read.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Retained for ease of use")]
    public class TLObject
    {
        #region Properties
        private JToken TLJson { get; set; } = null;
        #endregion
        #region Constructors
        /// <summary>
        /// Reads a serialized <see cref="TLObject"/> from a <see cref="BinaryReader"/>
        /// </summary>
        /// <param name="stream">Stream containing the serialized TLObject</param>
        public TLObject(BinaryReader stream)
        {
            TLJson = DeserializeObject(stream);
        }

        /// <summary>
        /// Loads a TLObject from a JSON object
        /// </summary>
        /// <param name="jObject"></param>
        public TLObject(JToken TLJson)
        {
            // ToDo: Add some verification?
            this.TLJson = TLJson;
        }
        #endregion
        #region Serialization
        /// <summary>
        /// Calculates a flag value based on the TLObject provided
        /// </summary>
        /// <param name="TLObject">The TLObject for which we should calculate the flags</param>
        /// <returns>The flag value as an int</returns>
        private int CalculateFlag()
        {
            // Make sure there is data to process and that it's of the correct type
            if (TLJson == null || TLJson["_"] == null || TLJson["_"].Type != JTokenType.String)
                throw new Exception("Cannot calculate the flag due to the current TLObject being invalid.");

            // Look up the TLObject's skeleton from the layer schema and fail if not found
            JObject TLSkeleton = FindConstructor((string)TLJson["_"]) ??
                throw new Exception("Cannot calculate the flag because the TLObject that was passed is not defined by the current schema.");

            // The master flag to be written
            int flag = 0;

            /*
             * Start a loop through all of the params and see if they are supposed to be
             * encoded in the flags so that they can be handled accordingly.
             * 
             */

            // Loop through the skeleton in case some flags are left out
            foreach (var f in TLSkeleton["params"])
            {
                // Parse the flag info
                Match flag_info = Regex.Match(f["type"].ToString(), @"flags\.(\d+)\?(.+)");
                // Skip everything that isn't supposed to be a included
                if (!flag_info.Success) continue;

                // This is the actual flag value to encode
                bool flagValue = false;
                // If the regex passed, this group will always be numeric
                int power = int.Parse(flag_info.Groups[1].Value);

                // If the value wasn't provided, we can assume it's false
                if (TLJson[f["name"].ToString()] != null && TLJson[f["name"].ToString()].Type != JTokenType.Null)
                {
                    // Determine how to process
                    switch (flag_info.Groups[2].Value)
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
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Boolean
                                && (bool)TLJson[f["name"].ToString()];
                            break;
                        case "int":
                            // ToDo: Parse the string representation of an int
                            // ToDo: Since long values are also considered integers, should we check for max size?
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Integer;
                            break;
                        case "int128":
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Bytes;
                            break;
                        case "int256":
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Bytes;
                            break;
                        case "Bool":
                            // ToDo: Parse the string and int representations of a bool
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Boolean;
                            break;
                        case "string":
                            // NOTE: We care about zero-length/empty strings.  Don't test for that.
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.String;
                            break;
                        case "bytes":
                            // ToDo: Parse a hex string and numeric representations of a byte array
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Bytes;
                            break;
                        case "long":
                            /* 
                             * ToDo: Parse a hex string representation of a long
                             * NOTE: Unfortunately, the Long type is lumped in with the Integer
                             * type.  It would be more aptly named "Number" but we work with what
                             * we have.
                             * 
                             */
                            flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Integer;
                            break;
                        default:
                            /*
                             * At this point, it could be:
                             *  ● A Vector type, which would be considered an array
                             *  ● A TLObject type
                             *  
                             */

                            // We wouldn't normally have to check, but sometimes the schema uses different cases
                            if (flag_info.Groups[2].Value.ToLower().StartsWith("vector"))
                            {
                                // NOTE: We care about empty arrays, too.  Don't test for that.
                                flagValue = TLJson[f["name"].ToString()].Type == JTokenType.Array;
                                break;
                            }

                            /*
                             * This is a little bit different as well because we are going to check
                             * the type of object contained.
                             * 
                             */

                            // The value has to be an object and the predicate must be a string
                            if (TLJson[f["name"].ToString()].Type != JTokenType.Object
                                || TLJson[f["name"].ToString()]["_"] == null
                                || TLJson[f["name"].ToString()]["_"].Type != JTokenType.String)
                                throw new Exception($"The value passed for \"{TLJson[f["name"].ToString()].Path}\" does not appear to be valid.  It should be a \"{flag_info.Groups[2].Value}\" type.");

                            // We need to be able to get the object
                            var return_type = FindConstructor((string)TLJson[f["name"].ToString()]["_"]) ??
                                throw new Exception($"Cannot find the TLObject for \"{(string)TLJson[f["name"].ToString()]["_"]}\".  It should be a \"{flag_info.Groups[2].Value}\" type.  If you updated the schema, make sure to also update the json definitions.");

                            // The base objects must match (case insensitive)
                            flagValue = return_type["return_type"].ToString().ToLower() == flag_info.Groups[2].Value.ToLower();
                            break;
                    }
                }

                // Now that we know whether this flag should be true or false, set it accordingly
                flag = flagValue
                    ? (flag | (int)Math.Pow(2, power))      // Bitwise OR
                    : (flag & ~(int)Math.Pow(2, power));    // Negated (~) Bitwise AND
            }

            // After all that...
            return flag;
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given int
        /// </summary>
        private byte[] SerializeInt(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeInt(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given int
        /// </summary>
        private void SerializeInt(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as an int is not supported.  Are you missing a param?");

            // Numeric check
            // ToDo: Parse the string representation of an int
            // ToDo: Since long values are also considered integers, should we check for max size?
            if (token.Type != JTokenType.Integer)
                throw new Exception($"An integer was required but \"{token.Type.ToString()}\" was passed");

            // Passed.  Write to stream
            IntegerUtil.Serialize((int)token, stream);
        }


        /// <summary>
        /// Returns a TL Serialized byte[] of a given int128
        /// </summary>
        private byte[] SerializeInt128(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeInt128(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given int128
        /// </summary>
        private void SerializeInt128(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as an int128 is not supported.  Are you missing a param?");

            // Numeric check
            if (token.Type != JTokenType.Bytes || ((byte[])token).Length != 16)
                throw new Exception($"A byte[16] was required but \"{token.Type.ToString()}\" was passed");

            // Passed.  Write to stream
            Int128Util.Serialize((byte[])token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given int256
        /// </summary>
        private byte[] SerializeInt256(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeInt256(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given int256
        /// </summary>
        private void SerializeInt256(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as an int256 is not supported.  Are you missing a param?");

            // Numeric check
            if (token.Type != JTokenType.Bytes || ((byte[])token).Length != 32)
                throw new Exception($"A byte[32] was required but \"{token.Type.ToString()}\" was passed");

            // Passed.  Write to stream
            Int256Util.Serialize((byte[])token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given Bool
        /// </summary>
        private byte[] SerializeBool(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeBool(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given Bool
        /// </summary>
        private void SerializeBool(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as a Boolean is not supported.  Are you missing a param?");

            // Boolean check
            // ToDo: Parse the string and int representations of a bool
            if (token.Type != JTokenType.Boolean)
                throw new Exception($"A Boolean was required but \"{token.Type.ToString()}\" was passed");

            // Passed.  Write to stream
            BoolUtil.Serialize((bool)token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given string
        /// </summary>
        private byte[] SerializeString(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeString(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given string
        /// </summary>
        private void SerializeString(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as a String is not supported.  Are you missing a param?");

            // String check
            if (token.Type != JTokenType.String)
                throw new Exception($"A String was required but \"{token.Type.ToString()}\" was passed");

            // Passed.  Write to stream
            StringUtil.Serialize((string)token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given byte[]
        /// 
        /// Confusing, but remember *TL Serialized* which includes padding/length of bytes, etc
        /// </summary>
        private byte[] SerializeBytes(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeBytes(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given byte[]
        /// 
        /// Confusing, but remember *TL Serialized* which includes padding/length of bytes, etc
        /// </summary>
        private void SerializeBytes(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as a Byte String is not supported.  Are you missing a param?");

            // Bytes check
            // ToDo: Parse a hex string and numeric representations of a byte array
            if (token.Type != JTokenType.Bytes)
                throw new Exception($"A byte[] was required but \"{token.Type.ToString()}\" was passed");

            // Passed.  Write to stream
            BytesUtil.Serialize((byte[])token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given long
        /// </summary>
        private byte[] SerializeLong(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeLong(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given long
        /// </summary>
        private void SerializeLong(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as a Long is not supported.  Are you missing a param?");

            // Long check
            // ToDo: Parse a hex string representation of a long
            if (token.Type != JTokenType.Integer)
                throw new Exception($"A Long was required but \"{token.Type.ToString()}\" was passed");

            // Passed.  Write to stream
            LongUtil.Serialize((long)token, stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given TLObject
        /// </summary>
        private byte[] SerializeObject(JToken token)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeObject(token, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given TLObject
        /// </summary>
        private void SerializeObject(JToken token, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as a TLObject is not supported.  Are you missing a param?");

            // Object check.
            // ToDo: Can we compare against known TLObjects to ensure it's validity?
            if (token.Type != JTokenType.Object
                || token["_"] == null
                || token["_"].Type != JTokenType.String)
                throw new Exception($"A TLObject was required but \"{token.Type.ToString()}\" was passed");

            // Call Serialize() on it's own object
            new TLObject(token).Serialize(stream);
        }

        /// <summary>
        /// Returns a TL Serialized byte[] of a given Vector
        /// </summary>
        private byte[] SerializeVector(JToken token, string type)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeVector(token, type, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given Vector
        /// </summary>
        private void SerializeVector(JToken token, string type, BinaryWriter stream)
        {
            // Null check
            if (token == null)
                throw new Exception($"Serializing 'null' as a Vector is not supported.  Are you missing a param?");

            // Make sure this is a Vector
            if (token.Type != JTokenType.Array)
                throw new Exception($"A Vector (array) was required but \"{token.Type.ToString()}\" was passed");

            // Make sure we have a vector type
            if (string.IsNullOrEmpty(type))
                throw new Exception($"A Vector (array) type was not specified and we cannot proceed.  Did the layer change and introduce issues?");

            // If we can't determine the Vector type, we won't know how to proceed.
            // NOTE: This assumes that the schema data is formatted correctly
            // ToDo: Are we not going to support the passing of normal values like 'long' or 'string'?
            //   Does it HAVE to incluve the vector string?
            Match vector_info = Regex.Match(type.ToLower(), "vector<(.+)>");
            if (!vector_info.Success)
                throw new Exception($"A Vector (array) type was not specified and we cannot proceed.  Did the layer change and introduce issues?");

            // Write a Vector constructor to the stream
            BuildTLObject("Vector").Serialize(stream);

            // Write the number of items
            IntegerUtil.Serialize(token.Count(), stream);

            // Loop through the number of items and process based on the item type
            for (var i = 0; i < token.Count(); i++)
            {
                switch (vector_info.Groups[1].Value)
                {
                    case "int":
                        SerializeInt(token[i], stream);
                        break;
                    case "int128":
                        SerializeInt128(token[i], stream);
                        break;
                    case "int256":
                        SerializeInt256(token[i], stream);
                        break;
                    case "Bool":
                        SerializeBool(token[i], stream);
                        break;
                    case "string":
                        SerializeString(token[i], stream);
                        break;
                    case "bytes":
                        SerializeBytes(token[i], stream);
                        break;
                    case "long":
                        SerializeLong(token[i], stream);
                        break;
                    default:
                        // ASSUME we are working with a TLObject...
                        // ToDo: Is this the best way to do it?  Will we ever have a Vector of Vector?
                        SerializeObject(token[i], stream);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Returns a TL Serialized byte[] of a given flag
        /// </summary>
        private byte[] SerializeFlag(JToken token, string type)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                SerializeFlag(token, type, writer);
                return memory.GetBuffer();
            }
        }
        /// <summary>
        /// Returns a TL Serialized byte[] of a given flag
        /// </summary>
        private void SerializeFlag(JToken token, string type, BinaryWriter stream)
        {
            // Skip params that are not provided and optional
            if (token == null || token.Type == JTokenType.Null) return;

            // Parse the flag info
            var flag_info = Regex.Match(type, @"flags\.(\d+)\?(.+)");
            if (!flag_info.Success)
                throw new Exception($"A flag type was not specified and we cannot proceed.  Did the layer change and introduce issues?");

            switch (flag_info.Groups[2].Value)
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

                    if (flag_info.Groups[2].Value.ToLower().StartsWith("vector"))
                    {
                        SerializeVector(token, flag_info.Groups[2].Value, stream);
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
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                Serialize(writer);
                return memory.ToArray();
            }
        }
        /// <summary>
        /// Writes a TLObject to a stream
        /// </summary>
        /// <param name="TLObject">Binary stream</param>
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
                if (TLJson == null || TLJson["_"] == null || TLJson["_"].Type != JTokenType.String)
                    throw new Exception($"Serializing 'null' as a TLObject is not supported.");

                // Look up the TLObject's skeleton from the layer schema and fail if not found
                var TLSkeleton = FindConstructor((string)TLJson["_"]) ??
                    throw new Exception("Unable to determine which TLObject we are serializing because the constructor cannot be matched with anything in the current layer.");

                // Write the constructor
                IntegerUtil.Serialize((int)TLSkeleton["id"], stream);

                // By looping through the params of the skeleton, we can know the order they 
                // should be written to the stream
                foreach (var SkeletonParam in TLSkeleton["params"])
                {
                    // Determine how to process this param
                    switch (SkeletonParam["type"].ToString())
                    {
                        case "#":
                            IntegerUtil.Serialize(CalculateFlag(), stream);
                            break;
                        case "int":
                            SerializeInt(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        case "int128":
                            SerializeInt128(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        case "int256":
                            SerializeInt256(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        case "Bool":
                            SerializeBool(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        case "string":
                            SerializeString(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        case "bytes":
                            SerializeBytes(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        case "long":
                            SerializeLong(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        case "!X":
                            // This could theoretically be any valid TLObject function
                            // ToDo: Check to make sure it's actually a function?
                            SerializeObject(TLJson[(string)SkeletonParam["name"]], stream);
                            break;
                        default:
                            /* 
                             * Here, we may be faced with one of the following:
                             *  ● An optional param based on a flag
                             *  ● A Vector type, which could be a Vector of anything
                             *  ● A TLObject type
                             *  ● A custom "array" type (specific to TLObjects)
                             */

                            if (SkeletonParam["type"].ToString().ToLower().StartsWith("vector"))
                            {
                                SerializeVector(TLJson[(string)SkeletonParam["name"]], (string)SkeletonParam["type"], stream);
                                break;
                            }
                            else if (SkeletonParam["type"].ToString().StartsWith("flags"))
                            {
                                SerializeFlag(TLJson[(string)SkeletonParam["name"]], (string)SkeletonParam["type"], stream);
                                break;
                            }

                            // There's nothing else this could be but a TLObject.
                            SerializeObject(TLJson[(string)SkeletonParam["name"]], stream);
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
        #endregion
        #region Deserialization
        /// <summary>
        /// Reads and returns a TL Serialized int as a JValue
        /// </summary>
        private static JValue DeserializeInt(BinaryReader stream)
        {
            return new JValue(IntegerUtil.Deserialize(stream));
        }
        /// <summary>
        /// Reads and returns a TL Serialized int128 as a JValue
        /// </summary>
        private static JValue DeserializeInt128(BinaryReader stream)
        {
            return new JValue(Int128Util.Deserialize(stream));
        }
        /// <summary>
        /// Reads and returns a TL Serialized int256 as a JValue
        /// </summary>
        private static JValue DeserializeInt256(BinaryReader stream)
        {
            return new JValue(Int256Util.Deserialize(stream));
        }
        /// <summary>
        /// Reads and returns a TL Serialized Bool as a JValue
        /// </summary>
        private static JValue DeserializeBool(BinaryReader stream)
        {
            return new JValue(BoolUtil.Deserialize(stream));
        }
        /// <summary>
        /// Reads and returns a TL Serialized string as a JValue
        /// </summary>
        private static JValue DeserializeString(BinaryReader stream)
        {
            return new JValue(StringUtil.Deserialize(stream));
        }
        /// <summary>
        /// Reads and returns a TL Serialized byte[] as a JValue
        /// </summary>
        private static JValue DeserializeBytes(BinaryReader stream)
        {
            return new JValue(BytesUtil.Deserialize(stream));
        }
        /// <summary>
        /// Reads and returns a TL Serialized long as a JValue
        /// </summary>
        private static JValue DeserializeLong(BinaryReader stream)
        {
            return new JValue(LongUtil.Deserialize(stream));
        }
        /// <summary>
        /// Reads and returns a TL Serialized flag as a JToken
        /// </summary>
        private static JToken DeserializeFlag(BinaryReader stream, int flag, string type)
        {
            /*
             * ToDo: An additional (optional) check would be to subtract the
             * bitwise AND result from the flag param.  If the flag param
             * is not 0 by the end of parsing, something went wrong.
             * 
             */

            // Parse the flag info
            Match flag_info = Regex.Match(type, @"flags\.(\d+)\?(.+)");
            if (!flag_info.Success) throw new Exception("Attempted to parse information about a flag in an unknown format.");

            // If the regex passed, this group will always be numeric
            int power = int.Parse(flag_info.Groups[1].Value);

            // If this is "true", it means that the param is a bool encoded into the flag.  We can
            // process here and skip the rest of the code.
            if (flag_info.Groups[2].Value == "true")
                // If the bitwise AND operation is 0, the result is false.  Thus, anything other than 0 is true.
                return new JValue((flag & (int)Math.Pow(2, power)) != 0);

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
            switch (flag_info.Groups[2].Value)
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

                    if (flag_info.Groups[2].Value.ToLower().StartsWith("vector"))
                        return DeserializeVector(stream, flag_info.Groups[2].Value);

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
                throw new Exception("Attempted to parse a Vector that wasn't a Vector.");

            // Parse the vector info
            Match vector_info = Regex.Match(type, "Vector<(.+)>", RegexOptions.IgnoreCase);
            if (!vector_info.Success) throw new Exception("Attempted to parse information about a vector in an unknown format.");
            
            // Create the object based on how many items are supposed to be in the stream
            JArray vector = new JArray();

            int VectorCount = IntegerUtil.Deserialize(stream);

            for (int i = 0; i < VectorCount; i++)
            {
                // Deserialize as needed
                switch (vector_info.Groups[1].Value)
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
             * 
             */

            try
            {
                var Constructor = IntegerUtil.Deserialize(stream);

                if (Enum.IsDefined(typeof(ManualTypes.Constructors), (uint)Constructor))
                {
                    return ManualTypes.Parse(stream, (ManualTypes.Constructors)Constructor);
                }

                // Look up the TLObject's skeleton from the layer schema and fail if not found
                JObject TLSkeleton = FindConstructor(Constructor) ??
                    throw new Exception("Unable to deserialize the TLObject because the constructor cannot be matched with anything in the current layer.");

                // Create the object to return
                JToken TLObject = JToken.FromObject(new
                {
                    _ = TLSkeleton["name"]
                });

                // In the case that we are deserializing a vector
                // we cannot loop through the skeleton because the
                // skeleton has no info...
                if (TLSkeleton["name"].ToString().Contains("vector"))
                {
                    stream.BaseStream.Position -= 4;
                    var v = DeserializeVector(stream, "vector<unknown>");
                    return v;
                }

                // By looping through the params of the skeleton, we can know the order they 
                // will be found in the stream
                foreach (JToken SkeletonParam in TLSkeleton["params"])
                {
                    // Determine how to process this param
                    switch ((string)SkeletonParam["type"])
                    {
                        // Read from the stream as specified by the param type
                        case "#":
                        case "int":
                            TLObject[(string)SkeletonParam["name"]] = DeserializeInt(stream);
                            break;
                        case "int128":
                            TLObject[(string)SkeletonParam["name"]] = DeserializeInt128(stream);
                            break;
                        case "int256":
                            TLObject[(string)SkeletonParam["name"]] = DeserializeInt256(stream);
                            break;
                        case "Bool":
                            TLObject[(string)SkeletonParam["name"]] = DeserializeBool(stream);
                            break;
                        case "string":
                            TLObject[(string)SkeletonParam["name"]] = DeserializeString(stream);
                            break;
                        case "bytes":
                            TLObject[(string)SkeletonParam["name"]] = DeserializeBytes(stream);
                            break;
                        case "long":
                            TLObject[(string)SkeletonParam["name"]] = DeserializeLong(stream);
                            break;
                        default:
                            /* 
                             * Here, we may be faced with one of the following:
                             *  ● An optional param based on a flag
                             *  ● A Vector type, which could be a Vector of anything
                             *  ● A TLObject type
                             *  ● A custom "array" type (specific to TLObjects)
                             */

                            // Start with flags
                            if (((string)SkeletonParam["type"]).StartsWith("flags"))
                            {
                                // Since we are deserializing, we assume that we read flags correctly
                                TLObject[(string)SkeletonParam["name"]] = DeserializeFlag(stream, (int)TLObject["flags"], (string)SkeletonParam["type"]);
                                break;
                            }
                            else if (((string)SkeletonParam["type"]).ToLower().StartsWith("vector"))
                            {
                                // It's a vector
                                TLObject[(string)SkeletonParam["name"]] = DeserializeVector(stream, (string)SkeletonParam["type"]);
                                break;
                            }

                            // Assume this is a TLObject and process accordingly
                            TLObject[(string)SkeletonParam["name"]] = DeserializeObject(stream);
                            break;
                    }
                }

                 Logger.Log(Logger.Level.Info, $"Successfully deserialized TLObject {TLObject["_"]}");

                return TLObject;
            }
            catch (Exception ex)
            {
                // If an error occured, return null.  There should be checks in place to determine if this happened
                Logger.Log(Logger.Level.Error, $"Unable to deserialize TLObject.\n\n{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads and returns a TLObject from a stream
        /// </summary>
        public static TLObject Deserialize(BinaryReader stream)
        {
            return new TLObject(DeserializeObject(stream));
        }
        public static TLObject Deserialize(byte[] data)
        {
            using (var memory = new MemoryStream(data))
            using (var reader = new BinaryReader(memory))
            {
                return new TLObject(reader);
            }
        }
        #endregion
        #region Static Methods
        /// <summary>
        /// Returns a TLObject based on the Constructor and arguments passed
        /// </summary>
        /// <param name="Constructor">The constructor as a signed integer</param>
        /// <param name="args">The arguments used to create the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        public static TLObject BuildTLObject(int Constructor, object args = null)
        {
            JToken jToken = FindConstructor(Constructor) ??
                throw new Exception($"Unknown constructor: \"{Constructor}\"");

            return BuildTLObject(jToken, args);
        }
        /// <summary>
        /// Returns a TLObject based on the Constructor and arguments passed
        /// </summary>
        /// <param name="Constructor">The constructor as an unsigned integer</param>
        /// <param name="args">The arguments used to create the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        public static TLObject BuildTLObject(uint Constructor, object args = null)
        {
            JToken jToken = FindConstructor(Constructor) ??
                throw new Exception($"Unknown constructor: \"{Constructor}\"");

            return BuildTLObject(jToken, args);
        }
        /// <summary>
        /// Returns a TLObject based on the Constructor and arguments passed
        /// </summary>
        /// <param name="Constructor">The constructor name (case insensitive)</param>
        /// <param name="args">The arguments used to create the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        public static TLObject BuildTLObject(string Constructor, object args = null)
        {
            JToken jToken = FindConstructor(Constructor) ??
                throw new Exception($"Unknown constructor: \"{Constructor}\"");

            return BuildTLObject(jToken, args);
        }
        /// <summary>
        /// Called internally, we know/assume that <paramref name="TLSkeleton"/> is valid.
        /// 
        /// Here we attempt to build the TLObject from the skeleton -- adding all the
        /// args as needed.
        /// 
        /// ToDo: Add conversion between cases like pascal, camel, and snake.  TLObjects
        /// always use snake case for conformity to the schema.
        /// </summary>
        /// <param name="TLSkeleton">The skeleton TLObject parsed from the layer schema</param>
        /// <param name="args">Arguments that make up the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        private static TLObject BuildTLObject(JToken TLSkeleton, object args = null)
        {
            // Assuming that the skeleton is valid
            JToken returns = JToken.FromObject(new
            {
                _ = TLSkeleton["name"],
            });

            /*
             * There is a point to be made for adding all params to the object in case
             * the param is not optional and intended to be left empty.  However, at this
             * time, only the params specified in the args variable are to be processed
             * and added.  If you would like a param to be empty, please specify that in
             * the arguments.
             * 
             */

            // Some objects don't have or need arguments
            if (args != null)
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Converters.Add(new InternalConverter());

                // Compile the items for easier access
                JToken jargs = JToken.FromObject(args, serializer);

                // Loop through each param in the skeleton and add IF provided by the user
                foreach (var p in TLSkeleton["params"])
                {
                    if (jargs[p["name"].ToString()] == null) continue;

                    /*
                     * Here's a question... what if the arg provided doesn't match the type
                     * required by the skeleton's param?
                     * 
                     * ToDo: Add SIMPLE type verification.
                     * 
                     */

                    // Assume that the arg is valid and add it
                    returns[p["name"].ToString()] = jargs[p["name"].ToString()];
                }
            }

            // Return whatever TLObject was compiled
            return new TLObject(returns);
        }

        //----------------------------------------------------------------------------------
        
        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="Constructor"></param>
        /// <returns></returns>
        public static JObject FindConstructor(int Constructor)
        {
            return FindConstructor((object)Constructor);
        }
        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="Constructor"></param>
        /// <returns></returns>
        public static JObject FindConstructor(string Constructor)
        {
            return FindConstructor((object)Constructor);
        }
        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="Constructor"></param>
        /// <returns></returns>
        public static JObject FindConstructor(uint Constructor)
        {
            return FindConstructor((object)Constructor);
        }
        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="Constructor"></param>
        /// <returns></returns>
        private static JObject FindConstructor(object Constructor)
        {
            var schema = TLSchema.Schema;

            if (Constructor is uint) Constructor = (int)Constructor;

            // Loop through the schema
            foreach (var c in schema)
            {
                // Skip the info
                if (c.Key == "schema_info") continue;


                // Loop through each section...
                foreach (JObject TL in schema[c.Key])
                {
                    // Compare the entry according to the type of the Constructor

                    if (TL["name"].ToString().ToLower() == Constructor.ToString().ToLower())
                    {
                        return TL;
                    }
                    else if (Convert.ToInt32(TL["hexid"].ToString(), 16).ToString() == Constructor.ToString())
                    {
                        return TL;
                    }
                    else if (TL["id"].ToString() == Constructor.ToString())
                    {
                        return TL;
                    }
                }
            }

            // If nothing is found, return null
            return null;
        }
        #endregion

        private JToken AsJToken()
        {
            return TLJson;
        }
        public static implicit operator JToken(TLObject o)
        {
            return o.AsJToken();
        }

        public JToken this[string key]
        {
            get
            {
                if (TLJson == null) return null;
                return TLJson[key];
            }
            set
            {
                if (TLJson == null)
                    TLJson = JObject.FromObject(new { key, value });
                else
                    TLJson[key] = value;
            }
        }
        public JToken this[int index]
        {
            get
            {
                if (TLJson == null) return null;
                return TLJson[index];
            }
            set
            {
                if (TLJson == null)
                    TLJson = JObject.FromObject(new { TLJson, value });
                else
                    TLJson[index] = value;
            }
        }
        public JTokenType InternalType => TLJson?.Type ?? JTokenType.Null;
        public override string ToString()
        {
            return TLJson.ToString();
        }

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