using BigMath;
using BigMath.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GlassTL.Telegram.Utils
{
    public static class IntegerUtil
    {
        public static int Deserialize(BinaryReader reader)
        {
            return reader.ReadInt32();
        }

        public static void Serialize(int src, BinaryWriter writer)
        {
            writer.Write(src);
        }
    }
    public static class BytesUtil
    {
        public static byte[] Read(BinaryReader binaryReader)
        {
            byte firstByte = binaryReader.ReadByte();
            int len, padding;
            if (firstByte == 254)
            {
                len = binaryReader.ReadByte() | (binaryReader.ReadByte() << 8) | (binaryReader.ReadByte() << 16);
                padding = len % 4;
            }
            else
            {
                len = firstByte;
                padding = (len + 1) % 4;
            }

            byte[] data = binaryReader.ReadBytes(len);
            if (padding > 0)
            {
                padding = 4 - padding;
                binaryReader.ReadBytes(padding);
            }

            return data;
        }
        public static BinaryWriter Write(BinaryWriter binaryWriter, byte[] data)
        {
            int padding;
            if (data.Length < 254)
            {
                padding = (data.Length + 1) % 4;
                if (padding != 0)
                {
                    padding = 4 - padding;
                }

                binaryWriter.Write((byte)data.Length);
            }
            else
            {
                padding = (data.Length) % 4;
                if (padding != 0)
                {
                    padding = 4 - padding;
                }

                binaryWriter.Write((byte)254);
                binaryWriter.Write((byte)(data.Length));
                binaryWriter.Write((byte)(data.Length >> 8));
                binaryWriter.Write((byte)(data.Length >> 16));
            }
            
            binaryWriter.Write(data);

            for (int i = 0; i < padding; i++)
            {
                binaryWriter.Write((byte)0);
            }

            return binaryWriter;
        }

        public static byte[] Deserialize(BinaryReader reader)
        {
            return Read(reader);
        }
        public static void Serialize(byte[] src, BinaryWriter writer)
        {
            Write(writer, src);
        }
        public static byte[] Serialize(byte[] src)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            Write(writer, src);
            return stream.ToArray();
        }
    }
    public static class StringUtil
    {
        public static string Read(BinaryReader reader)
        {
            byte[] data = BytesUtil.Read(reader);
            return Encoding.UTF8.GetString(data, 0, data.Length);
        }

        public static BinaryWriter Write(BinaryWriter writer, string str)
        {
            return BytesUtil.Write(writer, Encoding.UTF8.GetBytes(str));
        }

        public static string Deserialize(BinaryReader reader)
        {
            byte[] data = BytesUtil.Deserialize(reader);
            return Encoding.UTF8.GetString(data, 0, data.Length);
        }
        public static void Serialize(string src, BinaryWriter writer)
        {
            if (src == null) src = string.Empty;
            BytesUtil.Serialize(Encoding.UTF8.GetBytes(src), writer);
        }
    }
    public static class BoolUtil
    {
        public static bool Deserialize(BinaryReader reader)
        {
            var FalseCNumber = -1132882121;
            var TrueCNumber = -1720552011;
            var readed = reader.ReadInt32();
            if (readed == FalseCNumber) return false;
            else if (readed == TrueCNumber) return true;
            else throw new InvalidDataException(String.Format("Invalid Boolean Data : {0}", readed.ToString()));
        }
        public static void Serialize(bool src, BinaryWriter writer)
        {
            var FalseCNumber = -1132882121;
            var TrueCNumber = -1720552011;
            writer.Write(src ? TrueCNumber : FalseCNumber);
        }
    }
    public static class UIntUtil
    {
        public static uint Deserialize(BinaryReader reader)
        {
            return reader.ReadUInt32();
        }
        public static void Serialize(uint src, BinaryWriter writer)
        {
            writer.Write(src);
        }
    }
    public static class DoubleUtil
    {
        public static double Deserialize(BinaryReader reader)
        {
            return reader.ReadDouble();
        }
        public static void Serialize(double src, BinaryWriter writer)
        {
            writer.Write(src);
        }
    }
    public static class LongUtil
    {
        public static long Deserialize(BinaryReader reader)
        {
            return reader.ReadInt64();
        }
        public static void Serialize(long src, BinaryWriter writer)
        {
            writer.Write(src);
        }
    }
    public static class Int128Util
    {
        public static byte[] Deserialize(BinaryReader reader)
        {
            return reader.ReadBytes(16);
        }
        public static void Serialize(byte[] src, BinaryWriter writer)
        {
            writer.Write(src);
        }
    }
    //public class Int128Util
    //{
    //    public static Int128 Deserialize(BinaryReader reader)
    //    {
    //        return reader.ReadBytes(16).ToInt128(0, true);
    //    }
    //    public static void Serialize(Int128 src, BinaryWriter writer)
    //    {
    //        writer.Write(src.ToBytes(true));
    //    }
    //}
    public static class Int256Util
    {
        public static Int256 Deserialize(BinaryReader reader)
        {
            return reader.ReadBytes(32).ToInt256(0, true);
        }
        public static void Serialize(Int256 src, BinaryWriter writer)
        {
            writer.Write(src.ToBytes(true));
        }
        public static void Serialize(byte[] src, BinaryWriter writer)
        {
            writer.Write(src);
        }
    }

    public static class SchemaUtil
    {
        internal enum State
        {
            unknown,
            method,
            constructor,
        }
        public static JObject GetUpdatedSchema()
        {
            // https://raw.githubusercontent.com/telegramdesktop/tdesktop/dev/Telegram/Resources/scheme.tl

            string[] scheme_links =
            {
                "https://raw.githubusercontent.com/telegramdesktop/tdesktop/dev/Telegram/Resources/tl/mtproto.tl",
                "https://raw.githubusercontent.com/telegramdesktop/tdesktop/dev/Telegram/Resources/tl/api.tl"
            };

            JObject FullSchema = JObject.FromObject(new
            {
                schema_info = new JObject(),
                methods = new JArray(),
                constructors = new JArray(),
                unknown = new JArray()
            });

            using (var www = new WebClient())
            foreach (var link in scheme_links)
            {
                string text = www.DownloadString(link);
                string r_identifier = @"([A-z0-9_\.]+)#([a-f0-9]+)", r_parameter = @"[^{]([A-z0-9_]+):([\!A-z0-9_\#\?\.<>]+)", r_returnType = "= (.+);";
                State _state = State.constructor;
                bool isAuthorization = false;
                List<string> reserved = new List<string>();

                JObject schema = JObject.FromObject(new
                {
                    schema_info = new JObject(),
                    methods = new JArray(),
                    constructors = new JArray(),
                    unknown = new JArray()
                });

                foreach (string line in text.Split('\n'))
                {
                    if (line.IndexOf("// LAYER") == 0)
                    {
                        JObject schema_info = JObject.FromObject(new
                        {
                            layer = int.Parse(line.Split(' ')[2]),
                            parsed_from = link,
                            parse_date = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                            reserved
                        });
                    }

                    if (Regex.IsMatch(line, @"^\/+\s")) isAuthorization = line.Contains("Authorization");
                    if (line.IndexOf("//") == 0 || string.IsNullOrEmpty(line.Trim())) continue;
                    if (line.IndexOf("---functions---") == 0) { _state = State.method; continue; }
                    if (line.IndexOf("---types---") == 0) { _state = State.constructor; continue; }

                    if (!Regex.Match(line, r_identifier).Success) { ((JArray)schema["unknown"]).Add(line); continue; }

                    JObject current = JObject.FromObject(new
                    {
                        name = Regex.Match(line, r_identifier).Groups[1].Value,
                        id = Convert.ToInt32(Regex.Match(line, r_identifier).Groups[2].Value, 16),
                        hexid = Regex.Match(line, r_identifier).Groups[2].Value,
                        type = Enum.GetName(typeof(State), _state),
                        @params = new JArray(),
                        return_type = Regex.Match(line, r_returnType).Groups[1].Value
                    });

                    Match reservedMatch = Regex.Match(current["name"].ToString(), @"(\w+)\.\w+");

                    if (reservedMatch.Success && !reserved.Contains(reservedMatch.Groups[1].Value))
                    {
                        reserved.Add(reservedMatch.Groups[1].Value);
                    }

                    using (MTProto.Crypto.Crc32 crc32 = new MTProto.Crypto.Crc32())
                    {
                        string CrcInput = Regex.Replace(line, @"([\?:])bytes ", "$1string ");
                        CrcInput = Regex.Replace(CrcInput, @"(#\w+)|(\s\w+:flags\.\d+\?true)|[>{};]|(?<=v.{5})<(?![a-z])", string.Empty);
                        CrcInput = CrcInput.Replace("<", " ");
                        string hash = BitConverter.ToString(crc32.ComputeHash(Encoding.ASCII.GetBytes(CrcInput))).Replace("-", string.Empty).ToLower();

                        if (hash.TrimStart('0') != current["hexid"].ToString().TrimStart('0'))
                            Console.WriteLine($"{hash} was calculated, but it should have been {current["hexid"].ToString()}.");
                    }

                    foreach (Match m in Regex.Matches(line, r_parameter))
                    {
                        ((JArray)current["params"]).Add(JObject.FromObject(new
                        {
                            name = m.Groups[1].Value,
                            type = isAuthorization ? m.Groups[2].Value.Replace("string", "bytes") : m.Groups[2].Value
                        }));
                    }

                    ((JArray)schema[Enum.GetName(typeof(State), _state) + 's']).Add(current);
                }

                FullSchema.Add(schema);
            }

            return FullSchema;
        }

    }
}
