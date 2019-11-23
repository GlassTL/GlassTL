using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using GlassTL.Telegram.MTProto;

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

                //this.RawPeer[jProperty.Name] = jProperty.Value;
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
    }


    public class PeerManager
    {
        public enum ChatFolderFolder
        {
            Normal = 0,
            Archived = 1
        }

        private readonly List<PeerInfo> Peers = new List<PeerInfo>();

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
                    //if (!Peers[itemIndex].Min ) return;
                    //    if (UpdatedPeer.AccessHash == 0)
                    //{
                    //    //UpdatedPeer.AccessHash = Peers[itemIndex].AccessHash;
                    //}
                    //Peers[itemIndex] = UpdatedPeer;
                }
            }
            catch (Exception ex)
            {

            }
        }

        public PeerInfo? GetPeer(int ID)
        {
            var itemIndex = Peers.FindIndex(x => x.ID == ID);

            if (itemIndex != -1 && Peers[itemIndex].AccessHash != 0) return Peers[itemIndex];

            return null;
        }
    }
}
