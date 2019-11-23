//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace GlassTL.Telegram.MTProto.Crypto
//{
//    public class Salt : IComparable<Salt>
//    {
//        public Salt(int validSince, int validUntil, ulong salt)
//        {
//            ValidSince = validSince;
//            ValidUntil = validUntil;
//            Value = salt;
//        }

//        public int ValidSince { get; }

//        public int ValidUntil { get; }

//        public ulong Value { get; }

//        public int CompareTo(Salt other)
//        {
//            return ValidUntil.CompareTo(other.ValidSince);
//        }
//    }

//    public class SaltCollection
//    {
//        private SortedSet<Salt> salts;

//        public void Add(Salt salt)
//        {
//            salts.Add(salt);
//        }

//        public int Count
//        {
//            get
//            {
//                return salts.Count;
//            }
//        }
//        // TODO: get actual salt and other...
//    }

//    public class GetFutureSaltsResponse
//    {
//        public GetFutureSaltsResponse(ulong requestId, int now)
//        {
//            RequestId = requestId;
//            Now = now;
//        }

//        public void AddSalt(Salt salt)
//        {
//            Salts.Add(salt);
//        }

//        public ulong RequestId { get; }

//        public int Now { get; }

//        public SaltCollection Salts { get; }
//    }
//}
