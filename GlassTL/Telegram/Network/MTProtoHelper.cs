using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using GlassTL.Telegram.MTProto;
using GlassTL.Telegram.MTProto.Crypto;
using GlassTL.Telegram.Utils;

namespace GlassTL.Telegram.Network
{
    /// <summary>
    /// Contains all the logic for encrypting/decrypting messages, etc
    /// </summary>
    public class MTProtoHelper
    {
        public ServerAuthentication AuthInfo { get; internal set; } = null;
        public long Salt { get; set; } = 0;
        public long ID { get; private set; } = -1L;
        public int Sequence { get; set; } = -1;
        private long LastMessageID { get; set; } = -1;

        public dynamic Schema { get; } = new TLSchema();

        public MTProtoHelper(ServerAuthentication authinfo)
        {
            AuthInfo = authinfo ?? new ServerAuthentication();
            Reset();
        }

        /// <summary>
        /// Resets the state.
        /// </summary>
        public void Reset()
        {
            // Session IDs can be random on every connection
            ID            = BitConverter.ToInt64(Helpers.GenerateRandomBytes(8), 0);
            Sequence      = 0;
            LastMessageID = 0L;
        }

        /// <summary>
        /// Updates the message ID to a new one, used when the time offset changed.
        /// </summary>
        /// <param name="message"></param>
        public void UpdateMessageID(RequestState Message)
        {
            Message.MessageID = GetNewMessageID();
        }

        /// <summary>
        /// Calculates the key based on Telegram guidelines for MTProto 2.0,
        /// specifying whether it's the client or not. See
        /// https://core.telegram.org/mtproto/description#defining-aes-key-and-initialization-vector
        /// </summary>
        public static Tuple<byte[], byte[]> CalcKey(byte[] authKey, byte[] messageKey, bool client)
        {
            using (SHA256 sha = new SHA256Managed())
            {
                var offset = client ? 0 : 8;

                var sha256a = sha.ComputeHash(messageKey.Concat(authKey.Skip(offset).Take(36)).ToArray());
                var sha256b = sha.ComputeHash(authKey.Skip(offset + 40).Take(36).Concat(messageKey).ToArray());

                var aes_key = sha256a.Take(8)
                    .Concat(sha256b.Skip(8).Take(16))
                    .Concat(sha256a.Skip(24).Take(8))
                    .ToArray();

                var aes_iv = sha256b.Take(8)
                    .Concat(sha256a.Skip(8).Take(16))
                    .Concat(sha256b.Skip(24).Take(8))
                    .ToArray();

                return new Tuple<byte[], byte[]>(aes_key, aes_iv);
            }
        }

        /// <summary>
        /// Writes a message containing the given data into buffer.  Returns the message id.
        /// </summary>
        public long WriteDataAsMessage(byte[] data, out byte[] serialized, bool ContentRelated, long after_id = -1)
        {
            var body = after_id == -1 ? data : ((TLObject)Schema.invokeAfterMsg(new { msg_id = after_id, query = data })).Serialize();

            //using (var memory = new MemoryStream())
            //{
            //    using (var gzip = new GZipStream(memory, CompressionMode.Compress))
            //    {
            //        gzip.Write(body, 0, body.Length);
            //    }

            //    // Only use if it's smaller
            //    if (memory.ToArray().Length < body.Length)
            //    {
            //        body = memory.ToArray();
            //    }
            //}

            var msg_id = GetNewMessageID();

            serialized = BitConverter.GetBytes(msg_id)
                .Concat(BitConverter.GetBytes(GetSequenceNumber(ContentRelated)))
                .Concat(BitConverter.GetBytes(body.Length))
                .Concat(body)
                .ToArray();

            return msg_id;
        }

        /// <summary>
        /// Encrypts the given message data using the current authorization key
        /// following MTProto 2.0 guidelines core.telegram.org/mtproto/description.
        /// </summary>
        public byte[] EncryptMessageData(byte[] data)
        {
            using var sha = new SHA256Managed();
            var MessageKey = Array.Empty<byte>();

            var padding = Helpers.PositiveMod(-(8 + 8 + data.Length), 16);
            if (padding < 12) padding += 16;

            data = BitConverter.GetBytes(Salt)
                .Concat(BitConverter.GetBytes(ID))
                .Concat(data)
                .Concat(Helpers.GenerateRandomBytes(padding))
                .ToArray();

            // Being substr(what, offset, length); x = 0 for client
            // "msg_key_large = SHA256(substr(auth_key, 88+x, 32) + pt + padding)"
            // "msg_key = substr (msg_key_large, 8, 16)"
            MessageKey = sha.ComputeHash(AuthInfo.AuthKey.Key
                .Skip(88).Take(32)
                .Concat(data)
                .ToArray())
                .Skip(8).Take(16)
                .ToArray();

            (var AES_Key, var AES_IV) = CalcKey(AuthInfo.AuthKey.Key, MessageKey, true);

            return BitConverter.GetBytes(AuthInfo.AuthKey.KeyID)
                .Concat(MessageKey)
                .Concat(AES.EncryptIGE(data, AES_Key, AES_IV))
                .ToArray();
        }

        /// <summary>
        /// Inverse of <see cref="EncryptMessageData"/> for incoming server messages.
        /// </summary>
        public TLObject DecryptMessageData(byte[] body, bool client = false)
        {
            if (body.Length < 8)
                throw new Exception("Cannot decrypt a message of 8 bytes.");

            using (var memory = new MemoryStream(body))
            using (var reader = new BinaryReader(memory))
            using (var sha = new SHA256Managed())
            {
                var serverKeyID = reader.ReadUInt64();
                if (serverKeyID != AuthInfo.AuthKey.KeyID)
                    throw new Exception($"Server replied with an invalid auth key: {serverKeyID}");

                var MessageKey = reader.ReadBytes(16);

                (var AES_Key, var AES_ID) = CalcKey(AuthInfo.AuthKey.Key, MessageKey, client);

                body = AES.DecryptIGE(reader.ReadBytes(body.Length - 24), AES_Key, AES_ID);

                // https://core.telegram.org/mtproto/security_guidelines
                // Sections "checking sha256 hash" and "message length"
                var SHAHash = sha.ComputeHash(AuthInfo.AuthKey.Key
                    .Skip(client ? 88 : 96).Take(32)
                    .Concat(body)
                    .ToArray());

                var SHASlice = SHAHash.Skip(8).Take(16);

                if (!SHASlice.SequenceEqual(MessageKey))
                    throw new Exception("The message key could not be validated.");
            }

            TLObject obj = null;
            byte[] RawObject = null;
            var remote_msg_id = -1L;
            var remote_sequence = -1L;

            using (var memory = new MemoryStream(body))
            using (var reader = new BinaryReader(memory))
            {
                // The salt could be 0 if we are starting anew and haven't received one yet
                if (Salt != LongUtil.Deserialize(reader) && Salt != 0)
                    throw new Exception("The salt could not be validated");

                if (ID != LongUtil.Deserialize(reader))
                    throw new Exception("The session ID could not be validated");

                remote_msg_id   = LongUtil.Deserialize(reader);
                // ToDo: Check sequence_number
                remote_sequence = IntegerUtil.Deserialize(reader);
                RawObject = reader.ReadBytes(IntegerUtil.Deserialize(reader));

                obj = TLObject.Deserialize(RawObject);
            }

            return new TLObject(JToken.FromObject(new
            {
                _      = "Message",
                msg_id = remote_msg_id,
                seqno  = remote_sequence,
                bytes  = RawObject,
                body   = JToken.Parse(obj.ToString())
            }));
        }

        /// <summary>
        /// Generates a new unique message ID based on the current
        /// time(in ms) since epoch, applying a known timxe offset.
        /// </summary>
        /// <returns></returns>
        public long GetNewMessageID()
        {
            long time = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds);
            long newMessageId = ((time / 1000 + AuthInfo.TimeOffset) << 32) | ((time % 1000) << 22) | ((long)Helpers.GenerateRandomInt(524288) << 2); // 2^19
            // [ unix timestamp : 32 bit] [ milliseconds : 10 bit ] [ buffer space : 1 bit ] [ random : 19 bit ] [ msg_id type : 2 bit ] = [ msg_id : 64 bit ]

            if (LastMessageID >= newMessageId)
            {
                newMessageId = LastMessageID + 4;
            }

            LastMessageID = newMessageId;
            return newMessageId;
        }

        /// <summary>
        /// Updates the time offset to the correct one given a known valid message ID.
        /// </summary>
        public int UpdateTimeOffset(long CorrectMessageID)
        {
            //long bad = GetNewMessageID();
            var old = AuthInfo.TimeOffset;

            var now = (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            var correct = CorrectMessageID >> 32;

            AuthInfo.TimeOffset = (int)(correct - now);

            if (AuthInfo.TimeOffset != old) LastMessageID = 0;

            return AuthInfo.TimeOffset;
        }

        /// <summary>
        /// Generates the next sequence number depending on whether
        /// it should be for a content-related query or not.
        /// </summary>
        public int GetSequenceNumber(bool ContentRelated)
        {
            if (ContentRelated)
            {
                return Sequence++ * 2 + 1;
            }
            else
            {
                return Sequence * 2;
            }
        }

        /// <summary>
        /// Turns the current Helper into a serialized byte array
        /// </summary>
        public byte[] Serialize()
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                if (AuthInfo == null)
                {
                    BoolUtil.Serialize(false, writer);
                }
                else
                {
                    BoolUtil.Serialize(true, writer);

                    if (AuthInfo.AuthKey == null)
                    {
                        BoolUtil.Serialize(false, writer);
                    }
                    else
                    {
                        BoolUtil.Serialize(true, writer);
                        BytesUtil.Serialize(AuthInfo.AuthKey.Key, writer);
                    }

                    IntegerUtil.Serialize(AuthInfo.TimeOffset, writer);
                }

                return memory.ToArray();
            }
        }

        /// <summary>
        /// Deserilizes an MTProtoHelper object from serialized byte array
        /// </summary>
        /// <param name="raw">The serialized byte array containing the raw MTProtoHelper data</param>
        public static MTProtoHelper Deserialize(byte[] raw)
        {
            using (var memory = new MemoryStream(raw))
            using (var reader = new BinaryReader(memory))
            {
                return Deserialize(reader);
            }
        }

        /// <summary>
        /// Deserilizes an MTProtoHelper object from a stream
        /// </summary>
        /// <param name="reader">The stream containing the raw MTProtoHelper data</param>
        public static MTProtoHelper Deserialize(BinaryReader reader)
        {
            ServerAuthentication AuthInfo = null;

            if (BoolUtil.Deserialize(reader))
            {
                byte[] AuthInfoKey = null;

                if (BoolUtil.Deserialize(reader))
                {
                    AuthInfoKey = BytesUtil.Deserialize(reader);
                }

                AuthInfo = new ServerAuthentication()
                {
                    AuthKey = new AuthKey(AuthInfoKey),
                    TimeOffset = IntegerUtil.Deserialize(reader)
                };
            }

            return new MTProtoHelper(AuthInfo);
        }
    }
}
