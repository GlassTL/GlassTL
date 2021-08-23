namespace GlassTL.Telegram.MTProto
{
    using System.IO;
    using Newtonsoft.Json.Linq;
    using Utils;
    using System.IO.Compression;

    public static class ManualTypes
    {

        /// <summary>
        /// Manual constructors that should be parsed separately
        /// </summary>
        public enum Constructors : uint
        {
            MsgContainer = 0x73f1f8dc,
            GzipPacked = 0x3072cfa1,
            RpcResult = 0xf35c6d01
        }

        public enum RPCCodes : uint
        {
            RpcError = 0x2144ca19,
            GzipPacked = 0x3072cfa1
        }

        private static TLObject ParseRPCResult(BinaryReader reader)
        {
            try
            {
                var localMsgId = reader.ReadUInt64();
                var rpcCode = reader.ReadUInt32();

                var rawObject = JObject.FromObject(new
                {
                    _ = "rpc_result",
                    req_msg_id = localMsgId
                });

                switch ((RPCCodes)rpcCode)
                {
                    case RPCCodes.RpcError:
                        rawObject["result"] = JObject.FromObject(new {
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
                    case RPCCodes.GzipPacked:
                        rawObject["result"] = ParseGZipPacked(reader);
                        break;
                    default:
                        reader.BaseStream.Position -= 4;
                        rawObject["result"] = TLObject.Deserialize(reader);
                        break;
                }

                return new TLObject(rawObject);
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
                var rawObject = new JArray();
                var count = IntegerUtil.Deserialize(reader);

                for (var i = 0; i < count; i++)
                {
                    rawObject.Add(JObject.FromObject(new 
                    {
                        msg_id = LongUtil.Deserialize(reader),
                        seqno  = IntegerUtil.Deserialize(reader),
                        bytes  = IntegerUtil.Deserialize(reader),
                        body   = (JToken)TLObject.Deserialize(reader)
                    }));
                }
 
                return new TLObject(JObject.FromObject(new
                {
                    _ = "msg_container",
                    messages = rawObject
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
                using var unpackedStream = new MemoryStream();
                using var unzippedReader = new BinaryReader(unpackedStream);
                using var packedStream = new MemoryStream(BytesUtil.Read(reader), false);
                using var zippedStream = new GZipStream(packedStream, CompressionMode.Decompress);

                zippedStream.CopyTo(unpackedStream);
                unpackedStream.Position = 0;
                return TLObject.Deserialize(unzippedReader);
            }
            catch
            {
                return null;
            }
        }
        
        public static TLObject Parse(BinaryReader reader, Constructors constructor)
        {
            return constructor switch
            {
                Constructors.MsgContainer => ParseMessageContainer(reader),
                Constructors.RpcResult    => ParseRPCResult(reader),
                Constructors.GzipPacked   => ParseGZipPacked(reader),

                _ => null
            };
        }

        public static byte[] CreateMessageContainer(byte[][] messages)
        {
            Logger.Log(Logger.Level.Debug, $"Attempting to create a custom TLObject: {Constructors.MsgContainer}");

            using var memory = new MemoryStream(8);
            using var writer = new BinaryWriter(memory);

            IntegerUtil.Serialize((int)Constructors.MsgContainer, writer);
            IntegerUtil.Serialize(messages.Length, writer);

            foreach (var m in messages) writer.Write(m);

            Logger.Log(Logger.Level.Debug, $"TLObject {Constructors.MsgContainer} created.");
            return memory.ToArray();
        }
    }
}
