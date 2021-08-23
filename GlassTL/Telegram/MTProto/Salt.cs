namespace GlassTL.Telegram.MTProto
{
    using System;
    using System.Collections.Generic;

    public class Salt : IComparable<Salt>
    {
        public Salt(int validSince, int validUntil, ulong salt)
        {
            ValidSince = validSince;
            ValidUntil = validUntil;
            Value = salt;
        }

        public int ValidSince { get; }

        public int ValidUntil { get; }

        public ulong Value { get; }

        public int CompareTo(Salt other) => ValidUntil.CompareTo(other.ValidSince);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not Salt salt) return false;

            if (salt.ValidSince != ValidSince) return false;
            if (salt.ValidUntil != ValidUntil) return false;
            return salt.Value == Value;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(Salt left, Salt right)
        {
            if (left is null) return right is null;

            return left.Equals(right);
        }

        public static bool operator !=(Salt left, Salt right)
        {
            return !(left == right);
        }

        public static bool operator <(Salt left, Salt right)
        {
            return left is null ? right is object : left.CompareTo(right) < 0;
        }

        public static bool operator <=(Salt left, Salt right)
        {
            return left is null || left.CompareTo(right) <= 0;
        }

        public static bool operator >(Salt left, Salt right)
        {
            return left is object && left.CompareTo(right) > 0;
        }

        public static bool operator >=(Salt left, Salt right)
        {
            return left is null ? right is null : left.CompareTo(right) >= 0;
        }
    }

    public class SaltCollection
    {
        private readonly SortedSet<Salt> salts;

        public void Add(Salt salt) => salts.Add(salt);

        public int Count => salts.Count;
    }

    public class GetFutureSaltsResponse
    {
        public GetFutureSaltsResponse(ulong requestId, int now)
        {
            RequestId = requestId;
            Now = now;
        }

        public void AddSalt(Salt salt)
        {
            Salts.Add(salt);
        }

        public ulong RequestId { get; }

        public int Now { get; }

        public SaltCollection Salts { get; }
    }
}
