using System.Threading.Tasks;
using GlassTL.Telegram.MTProto;

namespace GlassTL.Telegram.Network
{
    public class RequestState
    {
        public long ContainerID { get; set; } = -1L;
        public long MessageID { get; set; } = -1L;

        public TLObject Request { get; private set; } = null;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "<Pending>")]
        public byte[] Data { get; private set; } = null;

        public TaskCompletionSource<TLObject> Response = null;

        public RequestState(TLObject Request)
        {
            this.Request = Request;
            Data = Request.Serialize();
            Response = new TaskCompletionSource<TLObject>();
        }
    }
}
