using System.IO;
using GlassTL.Telegram.Utils;
using GlassTL.Telegram.MTProto;
using GlassTL.Telegram.Network;

namespace GlassTL.Telegram
{
    public class Session
    {
        #region Public-Members
        /// <summary>
        /// Gets or sets a helper class for MTProto.
        /// 
        /// You should probably not edit this manually
        /// </summary>
        public MTProtoHelper Helper;

        public PeerManager KnownPeers { get; } = new PeerManager();

        /// <summary>
        /// Gets the name of this session.
        /// </summary>
        public string SessionName { get; }

        /// <summary>
        /// returns a TLObject of the currently signed-in user.  If no user is signed in, null is returned
        /// </summary>
        public TLObject TLUser { get; set; }

        /// <summary>
        /// Gets or sets the expiration date of the current session at which point the user must sign in again
        /// </summary>
        public int SessionExpires { get; set; }

        /// <summary>
        /// Gets or sets the dc currently associated with this session
        /// </summary>
        public DataCenter DataCenter { get; set; }
        #endregion

        #region Constructors-and-Factories
        public Session(string SessionName)
        {
            this.SessionName = SessionName;
        }
        #endregion

        #region Public-Methods
        /// <summary>
        /// Turns the current session into a serialized byte array
        /// </summary>
        public byte[] Serialize()
        {
            using var memory = new MemoryStream();
            using var writer = new BinaryWriter(memory);

            if (Helper == null)
            {
                BoolUtil.Serialize(false, writer);
            }
            else
            {
                BoolUtil.Serialize(true, writer);
                BytesUtil.Serialize(Helper.Serialize(), writer);
            }

            if (TLUser == null)
            {
                BoolUtil.Serialize(false, writer);
            }
            else
            {
                BoolUtil.Serialize(true, writer);
                TLUser.Serialize(writer);
            }

            IntegerUtil.Serialize(SessionExpires, writer);

            if (DataCenter == null)
            {
                BoolUtil.Serialize(false, writer);
            }
            else
            {
                BoolUtil.Serialize(true, writer);
                BytesUtil.Serialize(DataCenter.Serialize(), writer);
            }

            return memory.ToArray();
        }

        /// <summary>
        /// Deserilizes a Session object from serialized byte array
        /// </summary>
        /// <param name="raw">The serialized byte array containing the raw Session data</param>
        public static Session Deserialize(string FileName, byte[] raw)
        {
            using var memory = new MemoryStream(raw);
            using var reader = new BinaryReader(memory);

            return Deserialize(FileName, reader);
        }
        /// <summary>
        /// Deserilizes a Session object from a stream
        /// </summary>
        /// <param name="reader">The stream containing the raw Session data</param>
        public static Session Deserialize(string FileName, BinaryReader reader)
        {
            Network.MTProtoHelper Helper = null;

            if (BoolUtil.Deserialize(reader))
            {
                Helper = Network.MTProtoHelper.Deserialize(BytesUtil.Deserialize(reader));
            }

            TLObject TLUser = null;

            if (BoolUtil.Deserialize(reader))
            {
                TLUser = TLObject.Deserialize(reader);
            }

            var SessionExpires = IntegerUtil.Deserialize(reader);

            DataCenter DataCenter = null;

            if (BoolUtil.Deserialize(reader))
            {
                DataCenter = DataCenter.Deserialize(BytesUtil.Deserialize(reader));
            }

            return new Session(FileName)
            {
                Helper = Helper,
                TLUser = TLUser,
                SessionExpires = SessionExpires,
                DataCenter = DataCenter
            };
        }

        /// <summary>
        /// Saves the session to the disk
        /// </summary>
        public void Save()
        {
            using var stream = new FileStream($"{SessionName}.dat", FileMode.OpenOrCreate);

            var result = Serialize();
            stream.Write(result, 0, result.Length);
        }

        /// <summary>
        /// Attempts to load a session from the disk.  If the session does not exist, it is created
        /// </summary>
        /// <param name="FileName">The name of the session to load</param>
        public static Session LoadOrCreate(string FileName)
        {
            //return new Session(FileName);

            var sessionFileName = $"{FileName}.dat";

            if (!File.Exists(sessionFileName)) return new Session(FileName);

            using var stream = new FileStream(sessionFileName, FileMode.Open);
            using var reader = new BinaryReader(stream);

            return Deserialize(FileName, reader);
        }
        #endregion
    }
}
