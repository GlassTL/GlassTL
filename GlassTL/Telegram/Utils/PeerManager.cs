using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using GlassTL.Telegram.MTProto;
using System.IO;
using System.Diagnostics.CodeAnalysis;

namespace GlassTL.Telegram.Utils
{
    public struct PeerInfo
    {
        private JToken RawPeer { get; set; }
        public string Type => RawPeer.Value<string?>("_") ?? default;
        public int ID => RawPeer.Value<int?>("id") ?? default;
        public long AccessHash => RawPeer.Value<long?>("access_hash") ?? default;
        public bool Min => RawPeer.Value<bool?>("min") ?? default;
        public string FirstName => RawPeer.Value<string?>("first_name") ?? default;
        public string LastName => RawPeer.Value<string?>("last_name") ?? default;
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Title => RawPeer.Value<string?>("title") ?? default;

        public PeerInfo(JToken RawPeer)
        {
            this.RawPeer = RawPeer;
        }

        public void UpdatePeerInfo(JToken RawPeer)
        {
            foreach (JToken attribute in RawPeer)
            {
                var jProperty = attribute.ToObject<JProperty>();

                if (RawPeer.Value<bool>("min"))
                {
                    // If the peer doesn't contain full information,
                    // we don't want to update certain things because
                    // they will be empty anyway
                    if (jProperty.Name == "access_hash") continue;
                }

                if (this.RawPeer[jProperty.Name].ToString() != jProperty.Value.ToString())
                {
                    this.RawPeer[jProperty.Name] = jProperty.Value;
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(PeerInfo)) return false;

            return ((PeerInfo)obj).GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(PeerInfo left, PeerInfo right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(PeerInfo left, PeerInfo right)
        {
            return !(left == right);
        }

        public TLObject AsTLObject()
        {
            return new TLObject(RawPeer);
        }
    }


    public class PeerManager
    {
        public enum ChatFolderFolder
        {
            Normal = 0,
            Archived = 1
        }

        private readonly List<PeerInfo> Peers = new List<PeerInfo>();

        public void Clear()
        {
            Peers.Clear();
        }

        public void ParsePeers(TLObject message)
        {
            if (message == null) return;

            if (message["users"] != null && message["users"].Type == JTokenType.Array)
            {
                ((JArray)message["users"]).ToList().ForEach(x => AddOrUpdatePeer(x));
            }

            if (message["chats"] != null && message["chats"].Type == JTokenType.Array)
            {
                ((JArray)message["chats"]).ToList().ForEach(x => AddOrUpdatePeer(x));
            }
        }
        public void AddOrUpdatePeer(JToken peer)
        {
            try
            {
                if (peer.Value<string>("_") == "chatForbidden") return;
                var itemIndex = Peers.FindIndex(x => x.ID == peer.Value<int>("id"));

                var UpdatedPeer = new PeerInfo(peer);

                if (itemIndex == -1)
                {
                    Peers.Add(UpdatedPeer);
                }
                else
                {
                    Peers[itemIndex].UpdatePeerInfo(peer);
                }
            }
            catch (Exception ex)
            {

            }
        }
        public void AddOrUpdatePeers(PeerInfo[] peers)
        {
            peers.ToList().ForEach(x => AddOrUpdatePeer(x.AsTLObject()));
        }

        public PeerInfo? GetPeer(int ID)
        {
            var itemIndex = Peers.FindIndex(x => x.ID == ID);

            if (itemIndex != -1 && Peers[itemIndex].AccessHash != 0) return Peers[itemIndex];

            return null;
        }

        public byte[] Serialize()
        {
            using var memory = new MemoryStream();
            using var writer = new BinaryWriter(memory);

            IntegerUtil.Serialize(Peers.Count, writer);

            Peers.ForEach(x => new TLObject(x.AsTLObject()).Serialize(writer));

            return memory.ToArray();
        }

        /// <summary>
        /// Deserilizes a PeerManager object from serialized byte array
        /// </summary>
        /// <param name="raw">The serialized byte array containing the raw PeerManager data</param>
        public static PeerInfo[] Deserialize(byte[] raw)
        {
            using var memory = new MemoryStream(raw);
            using var reader = new BinaryReader(memory);

            return Deserialize(reader);
        }

        /// <summary>
        /// Deserilizes a PeerManager object from a stream
        /// </summary>
        /// <param name="reader">The stream containing the raw PeerManager data</param>
        public static PeerInfo[] Deserialize(BinaryReader reader)
        {
            return Enumerable.Range(0, IntegerUtil.Deserialize(reader))
                .Select(x => new PeerInfo(TLObject.Deserialize(reader)))
                .ToArray();
        }
    }
}
