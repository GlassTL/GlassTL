using System;
using System.IO;
using Newtonsoft.Json.Linq;
using GlassTL.Telegram.Utils;

namespace GlassTL.Telegram.MTProto
{
    public static class ManualTypes
    {

        public enum Constructors : uint
        {
            msg_container = 0x73f1f8dc,
            gzip_packed = 0x3072cfa1,
            rpc_result = 0xf35c6d01
        }

        public enum RPCCodes : uint
        {
            rpc_error = 0x2144ca19,
            gzip_packed = 0x3072cfa1
        }

        private static TLObject ParseRPCResult(BinaryReader reader)
        {
            try
            {
                ulong LocalMsgID = reader.ReadUInt64();
                uint RPCCode = reader.ReadUInt32();

                var RawObject = JObject.FromObject(new
                {
                    _ = "rpc_result",
                    req_msg_id = LocalMsgID
                });

                if (!Enum.IsDefined(typeof(RPCCodes), RPCCode))
                {
                    reader.BaseStream.Position -= 4;
                    RawObject["result"] = TLObject.Deserialize(reader);
                    return new TLObject(RawObject);
                }


                switch ((RPCCodes)RPCCode)
                {
                    case RPCCodes.rpc_error:
                        RawObject["result"] = JObject.FromObject(new {
                            _ = "rpc_error",
                            error_code = IntegerUtil.Deserialize(reader),
                            error_message = StringUtil.Read(reader)
                        });

                        //if (errorMessage.StartsWith("FLOOD_WAIT_"))
                        //{
                        //    var resultString = Regex.Match(errorMessage, @"\d+").Value;
                        //    var seconds = int.Parse(resultString);
                        //    throw new FloodException(TimeSpan.FromSeconds(seconds));
                        //}
                        //else if (errorMessage.StartsWith("PHONE_MIGRATE_"))
                        //{
                        //    var resultString = Regex.Match(errorMessage, @"\d+").Value;
                        //    var dcIdx = int.Parse(resultString);
                        //    throw new PhoneMigrationException(dcIdx);
                        //}
                        //else if (errorMessage.StartsWith("FILE_MIGRATE_"))
                        //{
                        //    var resultString = Regex.Match(errorMessage, @"\d+").Value;
                        //    var dcIdx = int.Parse(resultString);
                        //    throw new FileMigrationException(dcIdx);
                        //}
                        //else if (errorMessage.StartsWith("USER_MIGRATE_"))
                        //{
                        //    var resultString = Regex.Match(errorMessage, @"\d+").Value;
                        //    var dcIdx = int.Parse(resultString);
                        //    throw new UserMigrationException(dcIdx);
                        //}
                        //else if (errorMessage.StartsWith("NETWORK_MIGRATE_"))
                        //{
                        //    var resultString = Regex.Match(errorMessage, @"\d+").Value;
                        //    var dcIdx = int.Parse(resultString);
                        //    throw new NetworkMigrationException(dcIdx);
                        //}
                        //else if (errorMessage == "AUTH_RESTART")
                        //{
                        //    throw new AuthRestartException("The session is already logged in but is trying to log in again");
                        //}
                        //else if (errorMessage == "PHONE_CODE_INVALID")
                        //{
                        //    throw new InvalidPhoneCodeException("The numeric code used to authenticate does not match the numeric code sent by SMS/Telegram");
                        //}
                        //else if (errorMessage == "SESSION_PASSWORD_NEEDED")
                        //{
                        //    throw new CloudPasswordNeededException("This Account has Cloud Password !");
                        //}
                        //else
                        //{
                        //    throw new InvalidOperationException(errorMessage);
                        //}
                        break;
                    case RPCCodes.gzip_packed:
                        RawObject["result"] = JObject.Parse(ParseGZipPacked(reader).ToString());
                        break;
                    default:
                        reader.BaseStream.Position -= 4;
                        RawObject["result"] = JObject.Parse(TLObject.Deserialize(reader).ToString());
                        break;
                }

                return new TLObject(RawObject);
            }
            catch
            {
                return null;
            }
        }

        private static TLObject ParseMessageContainer(BinaryReader reader)
        {
            try
            {
                var RawObject = new JArray();
                var MessageCount = IntegerUtil.Deserialize(reader);

                for (int i = 0; i < MessageCount; i++)
                {
                    RawObject.Add(new JObject
                    {
                        ["msg_id"] = LongUtil.Deserialize(reader),
                        ["seqno"]  = IntegerUtil.Deserialize(reader),
                        ["bytes"]  = IntegerUtil.Deserialize(reader),
                        ["body"]   = TLObject.Deserialize(reader)
                    });
                }
 
                return new TLObject(JObject.FromObject(new
                {
                    _ = "msg_container",
                    messages = RawObject
                }));
            }
            catch
            {
                return null;
            }
        }

        private static TLObject ParseGZipPacked(BinaryReader reader)
        {
            try
            {
                using (var ms = new MemoryStream())
                using (var packedStream = new MemoryStream(BytesUtil.Read(reader), false))
                using (var zipStream = new System.IO.Compression.GZipStream(packedStream, System.IO.Compression.CompressionMode.Decompress))
                using (var compressedReader = new BinaryReader(ms))
                {
                    zipStream.CopyTo(ms);
                    ms.Position = 0;
                    return TLObject.Deserialize(compressedReader);
                }
            }
            catch
            {
                return null;
            }
        }

        public static TLObject Parse(BinaryReader reader, Constructors Constructor)
        {
            return Constructor switch
            {
                Constructors.msg_container => ParseMessageContainer(reader),
                Constructors.rpc_result => ParseRPCResult(reader),
                Constructors.gzip_packed => ParseGZipPacked(reader),

                _ => null
            };
        }

        public static byte[] CreateTypeSerialized(Constructors Constructor, JToken Data)
        {
            Logger.Log(Logger.Level.Debug, $"Attempting to create a custom TLObject: {Constructor.ToString()}");

            try
            {
                switch (Constructor)
                {
                    case Constructors.msg_container:
                        if (Data.Type != JTokenType.Array)
                        {
                            var sad = new System.Exception($"Invalid data was passed while serializing a Message Container.  Expected array but found {Data.Type.ToString()}");
                            Logger.Log(sad);
                            return null;
                        }

                        var messages = JArray.FromObject(Data);

                        using (var memory = new MemoryStream())
                        using (var writer = new BinaryWriter(memory))
                        {
                            IntegerUtil.Serialize((int)Constructors.msg_container, writer);
                            IntegerUtil.Serialize(messages.Count, writer);
                            foreach (var m in messages)
                            {
                                if (m.Type != JTokenType.Bytes)
                                {
                                    var sad = new Exception($"Invalid data was passed while serializing a Message Container.  Expected array of bytes but found array of {Data.Type.ToString()}");
                                    Logger.Log(sad);
                                    return null;
                                }

                                writer.Write((byte[])m);
                            }

                            Logger.Log(Logger.Level.Debug, $"TLObject {Constructor.ToString()} created.");
                            return memory.ToArray();
                        }
                    default:
                        return null;
                }

            }
            catch
            {
                return null;
            }
        }
    }
}
