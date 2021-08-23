using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GlassTL.Telegram.MTProto;
using BigMath;
using System.Runtime.CompilerServices;
using System.Security;
using System.Runtime.InteropServices;

namespace GlassTL.Telegram.Utils
{
    public static class Helpers
    {
        private static readonly Random random = new();
        private static long lastMessageId = 0;
        private static readonly char[][] LookupTableLower = Enumerable.Range(0, 256).Select(x => x.ToString("x2").ToCharArray()).ToArray();
        private static readonly char[][] LookupTableUpper = Enumerable.Range(0, 256).Select(x => x.ToString("X2").ToCharArray()).ToArray();

        /// <summary>
        /// Works just like the % (modulus) operator, but it returns always a postive number.
        /// </summary>
        public static int PositiveMod(int a, int b)
        {
            var result = a % b;

            return result < 0 ? result + Math.Abs(b) : result;
        }

        public static long GetNewMessageId(int timeOffset = 0)
        {
            var time = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;

            var newMessageId = ((time / 1000 + timeOffset) << 32) | ((time % 1000) << 22) | (long)(random.Next(524288) << 2); // 2^19
            // [ unix timestamp : 32 bit] [ milliseconds : 10 bit ] [ buffer space : 1 bit ] [ random : 19 bit ] [ msg_id type : 2 bit ] = [ msg_id : 64 bit ]

            if (lastMessageId >= newMessageId)
            {
                newMessageId = lastMessageId + 4;
            }

            lastMessageId = newMessageId;
            return newMessageId;
        }

        public static ulong GenerateRandomUlong()
        {
            ulong rand = (ulong)((random.Next()) << 32) | (ulong)random.Next();
            return rand;
        }
        public static long GenerateRandomLong()
        {
            long rand = (((long)random.Next()) << 32) | ((long)random.Next());
            return rand;
        }
        public static int GenerateRandomInt()
        {
            int rand = random.Next();
            return rand;
        }
        public static int GenerateRandomInt(int MaxValue)
        {
            int rand = random.Next(MaxValue);
            return rand;
        }
        public static int GenerateRandomInt(int MinValue, int MaxValue)
        {
            int rand = random.Next(MinValue, MaxValue);
            return rand;
        }

        public static byte[] GenerateRandomBytes(int num)
        {
            if (num == 0) return Array.Empty<byte>();

            using var rng = new RNGCryptoServiceProvider();
            var data = new byte[num];
            rng.GetBytes(data);
            return data;
        }

        //public static AESKeyData CalcKey(byte[] sharedKey, byte[] msgKey, bool client)
        //{
        //    int x = client ? 0 : 8;
        //    byte[] buffer = new byte[48];

        //    Array.Copy(msgKey, 0, buffer, 0, 16);            // buffer[0:16] = msgKey
        //    Array.Copy(sharedKey, x, buffer, 16, 32);     // buffer[16:48] = authKey[x:x+32]
        //    byte[] sha1a = Sha1(buffer);                     // sha1a = sha1(buffer)

        //    Array.Copy(sharedKey, 32 + x, buffer, 0, 16);   // buffer[0:16] = authKey[x+32:x+48]
        //    Array.Copy(msgKey, 0, buffer, 16, 16);           // buffer[16:32] = msgKey
        //    Array.Copy(sharedKey, 48 + x, buffer, 32, 16);  // buffer[32:48] = authKey[x+48:x+64]
        //    byte[] sha1b = Sha1(buffer);                     // sha1b = sha1(buffer)

        //    Array.Copy(sharedKey, 64 + x, buffer, 0, 32);   // buffer[0:32] = authKey[x+64:x+96]
        //    Array.Copy(msgKey, 0, buffer, 32, 16);           // buffer[32:48] = msgKey
        //    byte[] sha1c = Sha1(buffer);                     // sha1c = sha1(buffer)

        //    Array.Copy(msgKey, 0, buffer, 0, 16);            // buffer[0:16] = msgKey
        //    Array.Copy(sharedKey, 96 + x, buffer, 16, 32);  // buffer[16:48] = authKey[x+96:x+128]
        //    byte[] sha1d = Sha1(buffer);                     // sha1d = sha1(buffer)

        //    byte[] key = new byte[32];                       // key = sha1a[0:8] + sha1b[8:20] + sha1c[4:16]
        //    Array.Copy(sha1a, 0, key, 0, 8);
        //    Array.Copy(sha1b, 8, key, 8, 12);
        //    Array.Copy(sha1c, 4, key, 20, 12);

        //    byte[] iv = new byte[32];                        // iv = sha1a[8:20] + sha1b[0:8] + sha1c[16:20] + sha1d[0:8]
        //    Array.Copy(sha1a, 8, iv, 0, 12);
        //    Array.Copy(sha1b, 0, iv, 12, 8);
        //    Array.Copy(sha1c, 16, iv, 20, 4);
        //    Array.Copy(sha1d, 0, iv, 24, 8);

        //    return new AESKeyData(key, iv);
        //}

        //public static byte[] CalcMsgKey(byte[] data)
        //{
        //    byte[] msgKey = new byte[16];
        //    Array.Copy(Sha1(data), 4, msgKey, 0, 16);
        //    return msgKey;
        //}

        //public static byte[] CalcMsgKey(byte[] data, int offset, int limit)
        //{
        //    byte[] msgKey = new byte[16];
        //    Array.Copy(Sha1(data, offset, limit), 4, msgKey, 0, 16);
        //    return msgKey;
        //}

        //public static byte[] Sha1(byte[] data)
        //{
        //    using SHA1 sha1 = new SHA1Managed();
        //    return sha1.ComputeHash(data);
        //}

        //public static byte[] Sha1(byte[] data, int offset, int limit)
        //{
        //    using SHA1 sha1 = new SHA1Managed();
        //    return sha1.ComputeHash(data, offset, limit);
        //}

        public static byte[] Reverse(this byte[] input)
        {
            if (input == null) return null;
            var tmp = new byte[input.Length];
            Buffer.BlockCopy(input, 0, tmp, 0, input.Length);
            Array.Reverse(tmp);
            return tmp;
        }

        /// <summary>
        /// Attempts to invoke delegates on each handler's thread.  If that fails, invokation is done on the local thread.
        /// </summary>
        /// <param name="ev">The delegate to invoke</param>
        /// <param name="args">Arguments being passed to the delegate</param>
        public static void RaiseEventSafe(this Delegate ev, params object[] args)
        {
            RaiseEventSafe(ev, ref args);
        }
        
        /// <summary>
        /// Attempts to invoke delegates on each handler's thread.  If that fails, invokation is done on the local thread
        /// </summary>
        /// <param name="ev">The delegate to invoke</param>
        /// <param name="args">Arguments being passed to the delegate</param>
        public static void RaiseEventSafe(this Delegate ev, ref object[] args)
        {
            if (ev == null) return;

            // Loop through since we might have multiple handlers
            foreach (var singleCast in ev.GetInvocationList())
            {
                try
                {
                    // Determine if we can invoke on the handler's thread
                    if (singleCast.Target is ISynchronizeInvoke syncInvoke)
                    {
                        // Do so, if possible
                        syncInvoke.Invoke(singleCast, args);
                    }
                    else
                    {
                        // Otherwise, invoke on the current thread
                        singleCast.DynamicInvoke(args);
                    }
                }
                catch
                {
                    // In the case, there's an error, it is either because
                    // the args don't match or the handler itself threw one.
                    // We can move on silently at this point.
                }
            }
        }

        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            // ToDo: https://johnthiriet.com/cancel-asynchronous-operation-in-csharp/
            using var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

            if (completedTask != task) throw new TimeoutException("The operation has timed out.");
            
            timeoutCancellationTokenSource.Cancel();
            return await task;  // Very important in order to propagate exceptions
        }
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, int timeout)
        {
            return await task.TimeoutAfter(TimeSpan.FromMilliseconds(timeout));
        }
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            // ToDo: https://johnthiriet.com/cancel-asynchronous-operation-in-csharp/
            using var timeoutCancellationTokenSource = new CancellationTokenSource();
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

            if (completedTask != task) throw new TimeoutException("The operation has timed out.");

            timeoutCancellationTokenSource.Cancel();
            await task; // Very important in order to propagate exceptions
        }
        public static async Task TimeoutAfter(this Task task, int timeout)
        {
            await task.TimeoutAfter(TimeSpan.FromMilliseconds(timeout));
        }

        public static T[] Join<T>(this IEnumerable<T[]> arrays, T separator)
        {
            // Make sure we only iterate over arrays once
            var list = arrays.ToList();
            if (list.Count == 0) return Array.Empty<T>();

            // Aggregate() should be faster than Sum(lambda) due to overhead created by Sum
            var size = list.Aggregate(0, (result, element) => result + element.Length);
            var ret = new T[size + list.Count - 1];
            var index = 0;
            var first = true;

            foreach (var array in list)
            {
                if (!first) ret[index++] = separator;
                Array.Copy(array, 0, ret, index, array.Length);
                index += array.Length;
                first = false;
            }

            return ret;
        }
        public static T[] Join<T>(this IEnumerable<T[]> arrays)
        {
            // Make sure we only iterate over arrays once
            var list = arrays.ToList();
            if (list.Count == 0) return Array.Empty<T>();

            // Aggregate() should be faster than Sum(lambda) due to overhead created by Sum
            var size = list.Aggregate(0, (result, element) => result + element.Length);
            var ret = new T[size];
            var index = 0;

            foreach (var array in list)
            {
                Array.Copy(array, 0, ret, index, array.Length);
                index += array.Length;
            }

            return ret;
        }

        public static T[] SubArray<T>(this T[] array, int startIndex)
        {
            return SubArray(array, startIndex, array.Length - startIndex);
        }
        public static T[] SubArray<T>(this T[] array, int startIndex, int count)
        {
            var ret = new T[count];

            for (var i = 0; i < count; i++)
            {
                ret[i] = array[startIndex + i];
            }

            return ret;
        }

        /// <summary>
        ///     Converts array of bytes to <see cref="Int256" />.
        /// </summary>
        /// <param name="bytes">An array of bytes.</param>
        /// <param name="offset">The starting position within <paramref name="bytes" />.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        /// <returns><see cref="Int256" /> value.</returns>
        public static Int256 ToInt256(this byte[] bytes, int offset = 0, bool? asLittleEndian = null)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length == 0) return 0;
            if (bytes.Length <= offset) throw new InvalidOperationException("Array length must be greater than offset.");

            var ale = GetIsLittleEndian(asLittleEndian);
            EnsureLength(ref bytes, 32, offset, ale);

            var a = bytes.ToUInt64(ale ? offset + 24 : offset, ale);
            var b = bytes.ToUInt64(ale ? offset + 16 : offset + 8, ale);
            var c = bytes.ToUInt64(ale ? offset + 8 : offset + 16, ale);
            var d = bytes.ToUInt64(ale ? offset : offset + 24, ale);

            return new Int256(a, b, c, d);
        }
        /// <summary>
        ///     Converts array of bytes to <see cref="Int128" />.
        /// </summary>
        /// <param name="bytes">An array of bytes.</param>
        /// <param name="offset">The starting position within <paramref name="bytes" />.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        /// <returns><see cref="Int128" /> value.</returns>
        public static Int128 ToInt128(this byte[] bytes, int offset = 0, bool? asLittleEndian = null)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length == 0) return 0;
            if (bytes.Length <= offset) throw new InvalidOperationException("Array length must be greater than offset.");

            var ale = GetIsLittleEndian(asLittleEndian);
            EnsureLength(ref bytes, 16, offset, ale);

            return new Int128(bytes.ToUInt64(ale ? offset + 8 : offset, ale), bytes.ToUInt64(ale ? offset : offset + 8, ale));
        }
        /// <summary>
        ///     Converts array of bytes to <see cref="ulong" />.
        /// </summary>
        /// <param name="bytes">An array of bytes.</param>
        /// <param name="offset">The starting position within <paramref name="bytes" />.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        /// <returns><see cref="ulong" /> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToUInt64(this byte[] bytes, int offset = 0, bool? asLittleEndian = null)
        {
            return (ulong)bytes.ToInt64(offset, asLittleEndian);
        }
        /// <summary>
        ///     Converts array of bytes to <see cref="long" />.
        /// </summary>
        /// <param name="bytes">An array of bytes.</param>
        /// <param name="offset">The starting position within <paramref name="bytes" />.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        /// <returns><see cref="long" /> value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToInt64(this byte[] bytes, int offset = 0, bool? asLittleEndian = null)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length == 0) return 0;
            if (bytes.Length <= offset) throw new InvalidOperationException("Array length must be greater than offset.");

            var ale = GetIsLittleEndian(asLittleEndian);
            EnsureLength(ref bytes, 8, offset, ale);

            return ale
                ? bytes[offset] | (long)bytes[offset + 1] << 8 | (long)bytes[offset + 2] << 16 | (long)bytes[offset + 3] << 24 | (long)bytes[offset + 4] << 32 |
                    (long)bytes[offset + 5] << 40 | (long)bytes[offset + 6] << 48 | (long)bytes[offset + 7] << 56
                : (long)bytes[offset] << 56 | (long)bytes[offset + 1] << 48 | (long)bytes[offset + 2] << 40 | (long)bytes[offset + 3] << 32 |
                    (long)bytes[offset + 4] << 24 | (long)bytes[offset + 5] << 16 | (long)bytes[offset + 6] << 8 | bytes[offset + 7];
        }
        private static bool GetIsLittleEndian(bool? asLittleEndian)
        {
            return asLittleEndian ?? BitConverter.IsLittleEndian;
        }
        private static void EnsureLength(ref byte[] bytes, int minLength, int offset, bool ale)
        {
            var bytesLength = bytes.Length - offset;
            if (bytesLength < minLength)
            {
                var b = new byte[minLength];
                Buffer.BlockCopy(bytes, offset, b, ale ? 0 : minLength - bytesLength, bytesLength);
                bytes = b;
            }
        }

        /// <summary>
        ///     Get length of serial non zero items.
        /// </summary>
        /// <param name="bytes">Array of bytes.</param>
        /// <param name="asLittleEndian">True - skip all zero items from high. False - skip all zero items from low.</param>
        /// <returns>Length of serial non zero items.</returns>
        public static int GetNonZeroLength(this byte[] bytes, bool? asLittleEndian = null)
        {
            bool ale = GetIsLittleEndian(asLittleEndian);

            if (ale)
            {
                int index = bytes.Length - 1;
                while ((index >= 0) && (bytes[index] == 0))
                {
                    index--;
                }
                index = index < 0 ? 0 : index;
                return index + 1;
            }
            else
            {
                int index = 0;
                while ((index < bytes.Length) && (bytes[index] == 0))
                {
                    index++;
                }
                index = index >= bytes.Length ? bytes.Length - 1 : index;
                return bytes.Length - index;
            }
        }
        /// <summary>
        ///     Trim zero items.
        /// </summary>
        /// <param name="bytes">Array of bytes.</param>
        /// <param name="asLittleEndian">True - trim from high, False - trim from low.</param>
        /// <returns>Trimmed array of bytes.</returns>
        public static byte[] TrimZeros(this byte[] bytes, bool? asLittleEndian = null)
        {
            bool ale = GetIsLittleEndian(asLittleEndian);

            int length = GetNonZeroLength(bytes, ale);

            var trimmed = new byte[length];
            Buffer.BlockCopy(bytes, ale ? 0 : bytes.Length - length, trimmed, 0, length);
            return trimmed;
        }

        /// <summary>
        ///     Converts an <see cref="Int128" /> value to a byte array.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        /// <param name="trimZeros">Trim zero bytes from left or right, depending on endian.</param>
        /// <returns>Array of bytes.</returns>
        public static byte[] ToBytes(this Int128 value, bool? asLittleEndian = null, bool trimZeros = false)
        {
            var buffer = new byte[16];
            value.ToBytes(buffer, 0, asLittleEndian);

            if (trimZeros)
            {
                buffer = buffer.TrimZeros(asLittleEndian);
            }

            return buffer;
        }
        /// <summary>
        ///     Converts an <see cref="Int128" /> value to an array of bytes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="buffer">An array of bytes.</param>
        /// <param name="offset">The starting position within <paramref name="buffer" />.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        public static void ToBytes(this Int128 value, byte[] buffer, int offset = 0, bool? asLittleEndian = null)
        {
            bool ale = GetIsLittleEndian(asLittleEndian);
            value.Low.ToBytes(buffer, ale ? offset : offset + 8, ale);
            value.High.ToBytes(buffer, ale ? offset + 8 : offset, ale);
        }
        /// <summary>
        ///     Converts <see cref="uint" /> to array of bytes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="asLittleEndian">Convert to little endian.</param>
        /// <returns>Array of bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBytes(this uint value, bool? asLittleEndian = null)
        {
            return unchecked((int)value).ToBytes(asLittleEndian);
        }
        /// <summary>
        ///     Converts <see cref="int" /> to array of bytes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="asLittleEndian">Convert to little endian.</param>
        /// <returns>Array of bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToBytes(this int value, bool? asLittleEndian = null)
        {
            var buffer = new byte[4];
            value.ToBytes(buffer, 0, asLittleEndian);
            return buffer;
        }
        /// <summary>
        ///     Converts <see cref="int" /> to array of bytes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="buffer">Buffer at least 4 bytes.</param>
        /// <param name="offset">The starting position within <paramref name="buffer" />.</param>
        /// <param name="asLittleEndian">Convert to little endian.</param>
        /// <returns>Array of bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes(this int value, byte[] buffer, int offset = 0, bool? asLittleEndian = null)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (asLittleEndian ?? BitConverter.IsLittleEndian)
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
            }
            else
            {
                buffer[offset] = (byte)(value >> 24);
                buffer[offset + 1] = (byte)(value >> 16);
                buffer[offset + 2] = (byte)(value >> 8);
                buffer[offset + 3] = (byte)value;
            }
        }
        /// <summary>
        ///     Converts <see cref="long" /> to array of bytes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="buffer">Buffer at least 8 bytes.</param>
        /// <param name="offset">The starting position within <paramref name="buffer" />.</param>
        /// <param name="asLittleEndian">Convert to little endian.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes(this long value, byte[] buffer, int offset = 0, bool? asLittleEndian = null)
        {
            if (asLittleEndian ?? BitConverter.IsLittleEndian)
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
                buffer[offset + 4] = (byte)(value >> 32);
                buffer[offset + 5] = (byte)(value >> 40);
                buffer[offset + 6] = (byte)(value >> 48);
                buffer[offset + 7] = (byte)(value >> 56);
            }
            else
            {
                buffer[offset] = (byte)(value >> 56);
                buffer[offset + 1] = (byte)(value >> 48);
                buffer[offset + 2] = (byte)(value >> 40);
                buffer[offset + 3] = (byte)(value >> 32);
                buffer[offset + 4] = (byte)(value >> 24);
                buffer[offset + 5] = (byte)(value >> 16);
                buffer[offset + 6] = (byte)(value >> 8);
                buffer[offset + 7] = (byte)value;
            }
        }
        /// <summary>
        ///     Converts <see cref="ulong" /> to array of bytes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="buffer">Buffer at least 8 bytes.</param>
        /// <param name="offset">The starting position within <paramref name="buffer" />.</param>
        /// <param name="asLittleEndian">Convert to little endian.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ToBytes(this ulong value, byte[] buffer, int offset = 0, bool? asLittleEndian = null)
        {
            unchecked((long)value).ToBytes(buffer, offset, asLittleEndian);
        }
        /// <summary>
        ///     Converts an <see cref="Int256" /> value to a byte array.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        /// <param name="trimZeros">Trim zero bytes from left or right, depending on endian.</param>
        /// <returns>Array of bytes.</returns>
        public static byte[] ToBytes(this Int256 value, bool? asLittleEndian = null, bool trimZeros = false)
        {
            var buffer = new byte[32];
            value.ToBytes(buffer, 0, asLittleEndian);

            if (trimZeros)
            {
                buffer = buffer.TrimZeros(asLittleEndian);
            }

            return buffer;
        }
        /// <summary>
        ///     Converts an <see cref="Int256" /> value to an array of bytes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="buffer">An array of bytes.</param>
        /// <param name="offset">The starting position within <paramref name="buffer" />.</param>
        /// <param name="asLittleEndian">Convert from little endian.</param>
        public static void ToBytes(this Int256 value, byte[] buffer, int offset = 0, bool? asLittleEndian = null)
        {
            bool ale = GetIsLittleEndian(asLittleEndian);

            value.D.ToBytes(buffer, ale ? offset : offset + 24, ale);
            value.C.ToBytes(buffer, ale ? offset + 8 : offset + 16, ale);
            value.B.ToBytes(buffer, ale ? offset + 16 : offset + 8, ale);
            value.A.ToBytes(buffer, ale ? offset + 24 : offset, ale);
        }

        /// <summary>
        ///     Converts array of bytes to hexadecimal string.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <param name="caps">Capitalize chars.</param>
        /// <param name="min">Minimum string length. 0 if there is no minimum length.</param>
        /// <param name="spaceEveryByte">Space every byte.</param>
        /// <param name="trimZeros">Trim zeros in the result string.</param>
        /// <returns>Hexadecimal string representation of the bytes array.</returns>
        public static string ToHexString(this byte[] bytes, bool caps = true, int min = 0, bool spaceEveryByte = false, bool trimZeros = false)
        {
            return new ArraySegment<byte>(bytes, 0, bytes.Length).ToHexString(caps, min, spaceEveryByte, trimZeros);
        }
        /// <summary>
        ///     Converts array of bytes to hexadecimal string.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <param name="caps">Capitalize chars.</param>
        /// <param name="min">Minimum string length. 0 if there is no minimum length.</param>
        /// <param name="spaceEveryByte">Space every byte.</param>
        /// <param name="trimZeros">Trim zeros in the result string.</param>
        /// <returns>Hexadecimal string representation of the bytes array.</returns>
        public static string ToHexString(this ArraySegment<byte> bytes, bool caps = true, int min = 0, bool spaceEveryByte = false, bool trimZeros = false)
        {
            var count = bytes.Count;
            if (count == 0 || bytes.Array == null) return string.Empty;

            var strLength = min;
            var bim = 0;
            
            if (trimZeros)
            {
                bim = count - 1;
                for (int i = 0; i < count; i++)
                {
                    if (bytes.Array[i + bytes.Offset] > 0)
                    {
                        bim = i;
                        int l = (count - i) * 2;
                        strLength = (l <= min) ? min : l;
                        break;
                    }
                }
            }
            else
            {
                strLength = count * 2;
                strLength = strLength < min ? min : strLength;
            }

            if (strLength == 0) return "0";

            int step = 0;
            if (spaceEveryByte)
            {
                strLength += (strLength / 2 - 1);
                step = 1;
            }

            var chars = new char[strLength];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = '0';
            }

            if (spaceEveryByte)
            {
                for (int i = 2; i < chars.Length; i += 3)
                {
                    chars[i] = ' ';
                }
            }

            var lookupTable = caps ? LookupTableUpper : LookupTableLower;
            var bi = count - 1;
            var ci = strLength - 1;
            while (bi >= bim)
            {
                char[] chb = lookupTable[bytes.Array[bytes.Offset + bi--]];
                chars[ci--] = chb[1];
                chars[ci--] = chb[0];
                ci -= step;
            }

            int offset = 0;
            if (trimZeros && strLength > min)
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    char c = chars[i];
                    if (c != '0' && c != ' ')
                    {
                        offset = i;
                        break;
                    }
                }
            }

            return new string(chars, offset, strLength - offset);
        }

        /// <summary>
        /// Converted from TDesktop.
        /// </summary>
        /// <param name="pq"></param>
        /// <param name="factorized"></param>
        /// <returns></returns>
        public static bool FindPq(byte[] pq, out JToken factorized)
        {
            var pqL = BitConverter.ToInt64(pq.Reverse(), 0);
            var pqSqrt = (long)Math.Sqrt(pqL);

            while (pqSqrt * pqSqrt > pqL) pqSqrt--;
            while (pqSqrt * pqSqrt < pqL) pqSqrt++;

            while (true)
            {
                var ySqr = pqSqrt * pqSqrt - pqL;
                var y = (long)Math.Sqrt(ySqr);

                while (y * y > ySqr) y--;
                while (y * y < ySqr) y++;

                if (ySqr == 0 || y + pqSqrt >= pqL)
                {
                    factorized = null;
                    return false;
                }

                if (y * y == ySqr)
                {
                    var p = pqSqrt + y;
                    var q = (pqSqrt > y) ? (pqSqrt - y) : (y - pqSqrt);

                    factorized = JToken.FromObject(new
                    {
                        pq,
                        min = BitConverter.GetBytes((int)Math.Min(p, q)).Reverse(),
                        max = BitConverter.GetBytes((int)Math.Max(p, q)).Reverse(),
                    });

                    return true;
                }

                pqSqrt++;
            }
        }
        public static Tuple<string, TLObject[]> ParseEntities(string Message)
        {
            using dynamic schema = new TLSchema();
            var Tags = new List<TLObject>();
            var ParsedText = "";

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(Message);

            foreach (var e in htmlDoc.DocumentNode.ChildNodes)
            {
                switch (e.Name)
                {
                    case "b":
                        Tags.Add(schema.messageEntityBold(new
                        {
                            offset = ParsedText.Length,
                            length = e.InnerLength
                        }));

                        break;
                    case "i":
                        Tags.Add(schema.messageEntityItalic(new
                        {
                            offset = ParsedText.Length,
                            length = e.InnerLength
                        }));

                        break;
                    case "code":
                        Tags.Add(schema.messageEntityCode(new
                        {
                            offset = ParsedText.Length,
                            length = e.InnerLength
                        }));

                        break;
                }

                ParsedText += e.InnerText;
            }
            return new Tuple<string, TLObject[]>(ParsedText, !Tags.Any() ? null : Tags.ToArray());
        }

        /// <summary>
        /// Determines if two byte arrays contain the same elements.  This is similar to SequenceEquals, but will run much faster
        /// </summary>
        /// <param name="input1">The first array</param>
        /// <param name="input2">The second array</param>
        /// <returns>True if the two arrays contain the same elements, otherwise false.</returns>
        public static bool DirectSequenceEquals(this byte[] input1, byte[] input2)
        {
            if (input1 == null && input2 == null) return true;
            if (input1 == null || input2 == null) return false;
            if (input1.Length != input2.Length) return false;

            return !input1.Where((t, i) => t != input2[i]).Any();
        }

        public static void UInt32_To_BE(this uint n, byte[] bs) => UInt32_To_BE(n, bs, 0);
        public static void UInt32_To_BE(uint n, byte[] bs, int off)
        {
            bs[off] = (byte)(n >> 24);
            bs[++off] = (byte)(n >> 16);
            bs[++off] = (byte)(n >> 8);
            bs[++off] = (byte)(n);
        }

        public static uint BE_To_UInt32(this byte[] bs)
        {
            var n = (uint)bs[0] << 24;
            n |= (uint)bs[1] << 16;
            n |= (uint)bs[2] << 8;
            n |= bs[3];
            return n;
        }
        public static uint BE_To_UInt32(this byte[] bs, int off)
        {
            var n = (uint)bs[off] << 24;
            n |= (uint)bs[++off] << 16;
            n |= (uint)bs[++off] << 8;
            n |= bs[++off];
            return n;
        }

        public static ulong BE_To_UInt64(this byte[] bs)
        {
            var hi = BE_To_UInt32(bs);
            var lo = BE_To_UInt32(bs, 4);
            return ((ulong)hi << 32) | lo;
        }
        public static ulong BE_To_UInt64(this byte[] bs, int off)
        {
            var hi = BE_To_UInt32(bs, off);
            var lo = BE_To_UInt32(bs, off + 4);
            return ((ulong)hi << 32) | lo;
        }

        public static void UInt64_To_BE(this ulong n, byte[] bs)
        {
            UInt32_To_BE((uint)(n >> 32), bs);
            UInt32_To_BE((uint)(n), bs, 4);
        }
        public static void UInt64_To_BE(this ulong n, byte[] bs, int off)
        {
            UInt32_To_BE((uint)(n >> 32), bs, off);
            UInt32_To_BE((uint)(n), bs, off + 4);
        }

        public static void UInt32_To_LE(this uint n, byte[] bs) => UInt32_To_LE(n, bs, 0);
        public static void UInt32_To_LE(this uint n, byte[] bs, int off)
        {
            bs[off] = (byte)(n);
            bs[++off] = (byte)(n >> 8);
            bs[++off] = (byte)(n >> 16);
            bs[++off] = (byte)(n >> 24);
        }

        public static uint LE_To_UInt32(this byte[] bs) => LE_To_UInt32(bs, 0);
        public static uint LE_To_UInt32(this byte[] bs, int off)
        {
            var n = (uint)bs[off];
            n |= (uint)bs[++off] << 8;
            n |= (uint)bs[++off] << 16;
            n |= (uint)bs[++off] << 24;
            return n;
        }

        public static ulong LE_To_UInt64(this byte[] bs) => LE_To_UInt64(bs, 0);
        public static ulong LE_To_UInt64(this byte[] bs, int off)
        {
            var lo = LE_To_UInt32(bs, off);
            var hi = LE_To_UInt32(bs, off + 4);
            return ((ulong)hi << 32) | lo;
        }

        public static void UInt64_To_LE(this ulong n, byte[] bs)
        {
            UInt32_To_LE((uint)(n), bs);
            UInt32_To_LE((uint)(n >> 32), bs, 4);
        }
        public static void UInt64_To_LE(this ulong n, byte[] bs, int off)
        {
            UInt32_To_LE((uint)(n), bs, off);
            UInt32_To_LE((uint)(n >> 32), bs, off + 4);
        }

        /// <summary>
        /// Pads a byte array to a fixed length or returns the array if already the correct size or larger
        /// </summary>
        /// <param name="buffer">The byte array to pad</param>
        /// <param name="desiredLength">The byte array to pad</param>
        public static byte[] PadByteArray(this byte[] buffer, int desiredLength)
        {
            // Can't pad nothing...
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            // Determine how many padding bytes we need
            var fill = desiredLength - buffer.Length;

            // If we don't need any padding, just return the array
            if (fill <= 0) return (byte[])buffer.Clone();

            // Create a new array of the correct size
            var result = new byte[desiredLength];
            // Copy the buffer into the new array at the correct index
            Array.Copy(buffer, 0, result, fill, buffer.Length);

            return result;
        }

        public static T Process<T>(this SecureString src, Func<byte[], T> func)
        {
            var binaryString = IntPtr.Zero;
            byte[] workArray = null;
            GCHandle? handle = null;
            try
            {
                /*** PLAINTEXT EXPOSURE BEGINS HERE ***/
                binaryString = Marshal.SecureStringToBSTR(src);
                unsafe
                {
                    var bstrBytes = (byte*)binaryString;
                    workArray = new byte[src.Length * 2];
                    handle = GCHandle.Alloc(workArray, GCHandleType.Pinned);
                
                    for (var i = 0; i < workArray.Length; i++) workArray[i] = *bstrBytes++;
                }

                return func(workArray);
            }
            finally
            {
                if (workArray != null) for (var i = 0; i < workArray.Length; i++) workArray[i] = 0;
                if (binaryString != IntPtr.Zero) Marshal.ZeroFreeBSTR(binaryString);

                handle?.Free();

                /*** PLAINTEXT EXPOSURE ENDS HERE ***/
            }
        }

        public static T ReturnWithException<T>(Exception ex, T returnValue)
        {
            Logger.Log(ex);
            return returnValue;
        }
        public static T ReturnWithException<T>(Exception ex)
        {
            Logger.Log(ex);
            return default;
        }
    }
}
