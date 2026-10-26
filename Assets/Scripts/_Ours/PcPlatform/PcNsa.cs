#if !SWITCH
// PC platform layer (s33c): nn::account network-service-account (NSA) calls and nn::account::AsyncContext. NOT shipped code.
// Behaviour reference: Ryujinx Services/Account/Acc (AccountService/ManagerServer.cs, IAsyncContext.cs, AsyncExecution.cs), which
// answers the same with guest internet on or off: the NSA check succeeds, the NSA id is 0xcafe, EnsureIdTokenCacheAsync completes
// at once with Success, and LoadIdTokenCache hands back a freshly signed RS256 JWT (random key, 3 h expiry). Whatever the game does
// with that token next needs the network, which the emulated console does not have (nifm / NEX fail there, not here).
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PcPlatform
{
    public static class PcNsa
    {
        const int Module = 124;
        // Ryujinx Account ResultCode.AsyncExecutionNotInitialized (124-40) / Unknown41 (124-41: the async call has not finished).
        public static readonly nn.Result AsyncExecutionNotInitialized = new nn.Result(Module, 40);
        public static readonly nn.Result AsyncNotDone = new nn.Result(Module, 41);

        // Ryujinx ManagerServer.NetworkServiceAccountId.
        public const ulong NetworkServiceAccountId = 0xcafe;

        // ---- nn::account::AsyncContext ----

        class Context
        {
            public bool Initialized;   // an async request was attached (Ryujinx AsyncExecution.IsInitialized)
            public bool Done;          // its system event is signalled
            public nn.Result Result;
        }

        static readonly Dictionary<long, Context> contexts = new Dictionary<long, Context>();
        static long nextContext = 0x2000;

        public static IntPtr CreateContext()
        {
            lock (contexts)
            {
                long handle = nextContext;
                nextContext += 0x10;
                contexts[handle] = new Context();
                return new IntPtr(handle);
            }
        }

        public static void DestroyContext(IntPtr context)
        {
            lock (contexts)
                contexts.Remove(context.ToInt64());
        }

        static Context Get(IntPtr context)
        {
            Context c;
            lock (contexts)
                contexts.TryGetValue(context.ToInt64(), out c);
            return c;
        }

        public static nn.Result Cancel(IntPtr context)
        {
            Context c = Get(context);
            if (c == null || !c.Initialized)
                return AsyncExecutionNotInitialized;
            return new nn.Result();
        }

        public static nn.Result HasDone(IntPtr context, ref bool done)
        {
            Context c = Get(context);
            if (c == null || !c.Initialized)
                return AsyncExecutionNotInitialized;
            done = c.Done;
            return new nn.Result();
        }

        public static nn.Result GetResult(IntPtr context)
        {
            Context c = Get(context);
            if (c == null || !c.Initialized)
                return AsyncExecutionNotInitialized;
            if (!c.Done)
                return AsyncNotDone;
            return c.Result;
        }

        // ---- nn::account network service account ----

        static bool IsUser(nn.account.UserHandle handle)
        {
            return handle._data0 == PcConfig.UserId0 && handle._data1 == PcConfig.UserId1;
        }

        // Ryujinx ManagerServer.CheckAvailability: Success.
        public static nn.Result EnsureAvailable(nn.account.UserHandle handle)
        {
            if (!IsUser(handle))
                return PcAccount.UserNotExist;
            return new nn.Result();
        }

        public static nn.Result IsAvailable(ref bool available, nn.account.UserHandle handle)
        {
            if (!IsUser(handle))
                return PcAccount.UserNotExist;
            available = true;
            return new nn.Result();
        }

        public static nn.Result GetId(ref nn.account.NetworkServiceAccountId id, nn.account.UserHandle handle)
        {
            if (!IsUser(handle))
                return PcAccount.UserNotExist;
            id.id = NetworkServiceAccountId;
            return new nn.Result();
        }

        // Ryujinx EnsureIdTokenCacheAsync: the async execution runs a completed task, the event is signalled, GetResult = Success.
        public static nn.Result EnsureIdTokenCacheAsync(IntPtr context, nn.account.UserHandle handle)
        {
            if (!IsUser(handle))
                return PcAccount.UserNotExist;
            Context c = Get(context);
            if (c == null)
                return new nn.Result(Module, 302); // Ryujinx ResultCode.NullObject
            c.Initialized = true;
            c.Done = true;
            c.Result = new nn.Result();
            return new nn.Result();
        }

        static byte[] cachedToken;
        static DateTime cachedTokenExpiry;

        // Ryujinx LoadIdTokenCache: the cached token (regenerated after 3 h) is written to the buffer, its length is the out size.
        public static nn.Result LoadIdTokenCache(ref ulong actualSize, byte[] buffer, int bufferSize, nn.account.UserHandle handle)
        {
            if (!IsUser(handle))
                return PcAccount.UserNotExist;
            if (cachedToken == null || DateTime.UtcNow > cachedTokenExpiry)
            {
                cachedTokenExpiry = DateTime.UtcNow + TimeSpan.FromHours(3);
                cachedToken = Encoding.ASCII.GetBytes(GenerateIdToken());
            }
            int n = Math.Min(cachedToken.Length, Math.Min(bufferSize, buffer == null ? 0 : buffer.Length));
            if (n > 0)
                Buffer.BlockCopy(cachedToken, 0, buffer, 0, n);
            actualSize = (ulong)cachedToken.Length;
            return new nn.Result();
        }

        // Ryujinx ManagerServer.GenerateIdToken: an RS256 JWT signed by a throw-away 2048-bit key with the same claims.
        static string GenerateIdToken()
        {
            using (RSA rsa = new RSACryptoServiceProvider(2048))
            {
                string hex16a = RandomHex(16), hex16b = RandomHex(16), hex16c = RandomHex(16);
                long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                string header = "{\"alg\":\"RS256\",\"kid\":\"System.Security.Cryptography.RSAParameters\",\"typ\":\"id_token\"}";
                string payload = "{\"sub\":\"" + hex16a + "\",\"jku\":\"https://e0d67c509fb203858ebcb2fe3f88c2aa.baas.nintendo.com/1.0.0/certificates\""
                    + ",\"di\":\"" + hex16b + "\",\"sn\":\"XAW10000000000\",\"bs:did\":\"" + hex16c + "\""
                    + ",\"aud\":\"ed9e2f05d286f7b8\",\"iss\":\"https://e0d67c509fb203858ebcb2fe3f88c2aa.baas.nintendo.com\""
                    + ",\"iat\":" + now + ",\"nbf\":" + now + ",\"exp\":" + (now + 3 * 3600) + "}";
                string signingInput = Base64Url(Encoding.UTF8.GetBytes(header)) + "." + Base64Url(Encoding.UTF8.GetBytes(payload));
                byte[] signature;
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(signingInput));
                    signature = ((RSACryptoServiceProvider)rsa).SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));
                }
                return signingInput + "." + Base64Url(signature);
            }
        }

        static string RandomHex(int bytes)
        {
            byte[] b = new byte[bytes];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(b);
            StringBuilder sb = new StringBuilder(bytes * 2);
            for (int i = 0; i < b.Length; i++)
                sb.Append(b[i].ToString("x2"));
            return sb.ToString();
        }

        static string Base64Url(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
#endif
