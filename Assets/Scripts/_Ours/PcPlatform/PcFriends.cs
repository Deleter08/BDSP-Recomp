#if !SWITCH
// PC platform layer (s33c): nn::friends with no friends and no network. NOT shipped code.
// Behaviour reference: Ryujinx Horizon/Sdk/Friends/Detail/Ipc/FriendService.cs + NotificationService.cs (the friends service answers
// the same with guest internet on or off): every list is empty (count 0, Success), the availability checks say "available",
// the Ensure* async calls complete at once with Success, presence / play-history calls succeed, a null Uid is InvalidArgument
// (121-2), and an empty notification queue pops NotificationNotFound (121-15). The friends applets (friend list, user page,
// friend requests, invitations) are logged and return Success, as when the player closes them.
// No Friend / FriendPresence / Profile value ever comes back filled on PC, so every one the game holds is the zero-filled
// struct, and the SDK accessors answer for that: id 0, empty nickname / description / URL, Offline, not valid / favourite / new.
using System;
using System.Collections.Generic;
using nn.account;

namespace PcPlatform
{
    public static class PcFriends
    {
        const int Module = 121;
        public static readonly nn.Result InvalidArgument = new nn.Result(Module, 2);
        public static readonly nn.Result NotCalled = new nn.Result(Module, 7);
        public static readonly nn.Result NotificationNotFound = new nn.Result(Module, 15);

        static bool initialized;

        public static void Initialize()
        {
            if (!initialized)
                PcConfig.Log("friends: initialized (no friends, offline)");
            initialized = true;
        }

        static Uid CurrentUser
        {
            get { return PcAccount.User; }
        }

        static nn.Result CheckUid(Uid uid)
        {
            return uid.IsValid() ? new nn.Result() : InvalidArgument;
        }

        // ---- lists ----

        public static nn.Result GetList(ref int outCount, Uid uid)
        {
            outCount = 0;
            return CheckUid(uid);
        }

        public static nn.Result GetList(ref int outCount)
        {
            return GetList(ref outCount, CurrentUser);
        }

        public static nn.Result CheckAvailability(ref bool outIsAvailable)
        {
            outIsAvailable = true;
            return new nn.Result();
        }

        // ---- nn::friends::AsyncContext ----

        class Context
        {
            public bool Called;
            public nn.Result Result;
        }

        static readonly Dictionary<long, Context> contexts = new Dictionary<long, Context>();
        static long nextContext = 0x3000;

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

        // An async request bound to the context: the service answers Success at once, so the context is done.
        public static nn.Result Complete(IntPtr context, nn.Result result)
        {
            Context c = Get(context);
            if (c != null)
            {
                c.Called = true;
                c.Result = result;
            }
            return new nn.Result();
        }

        public static nn.Result Cancel(IntPtr context)
        {
            Context c = Get(context);
            if (c == null || !c.Called)
                return NotCalled; // TODO(pc): SDK-internal answer for a context no request was bound to (ResultNotCalled assumed)
            return new nn.Result();
        }

        public static nn.Result HasDone(IntPtr context, ref bool outDone)
        {
            Context c = Get(context);
            if (c == null || !c.Called)
                return NotCalled;
            outDone = true;
            return new nn.Result();
        }

        public static nn.Result GetResult(IntPtr context)
        {
            Context c = Get(context);
            if (c == null || !c.Called)
                return NotCalled;
            return c.Result;
        }

        // ---- play history ----

        // Ryujinx GetPlayHistoryRegistrationKey: Type 0x101, KeyIndex = random & 7, the local-play flag, a random UUID, a zero HMAC.
        public static unsafe nn.Result GetPlayHistoryRegistrationKey(ref nn.friends.PlayHistoryRegistrationKey outKey, Uid uid, bool isLocalPlay)
        {
            if (!uid.IsValid())
            {
                outKey = default(nn.friends.PlayHistoryRegistrationKey);
                return InvalidArgument;
            }
            byte[] key = new byte[64];
            key[0] = 0x01;
            key[1] = 0x01;
            key[2] = (byte)(new Random().Next() & 7);
            key[3] = 0;
            key[4] = (byte)(isLocalPlay ? 1 : 0);
            Guid.NewGuid().ToByteArray().CopyTo(key, 0x10);
            fixed (nn.friends.PlayHistoryRegistrationKey* p = &outKey)
            {
                byte* dst = (byte*)p;
                for (int i = 0; i < 64; i++)
                    dst[i] = key[i];
            }
            return new nn.Result();
        }

        public static nn.Result GetPlayHistoryRegistrationKey(ref nn.friends.PlayHistoryRegistrationKey outKey, bool isLocalPlay)
        {
            return GetPlayHistoryRegistrationKey(ref outKey, CurrentUser, isLocalPlay);
        }

        // ---- notifications ----

        class Queue
        {
            public Uid User;
        }

        static readonly Dictionary<long, Queue> queues = new Dictionary<long, Queue>();
        static long nextQueue = 0x4000;

        public static IntPtr CreateQueue()
        {
            lock (queues)
            {
                long handle = nextQueue;
                nextQueue += 0x10;
                queues[handle] = new Queue();
                return new IntPtr(handle);
            }
        }

        public static void DestroyQueue(IntPtr queue)
        {
            lock (queues)
                queues.Remove(queue.ToInt64());
        }

        static Queue GetQueue(IntPtr queue)
        {
            Queue q;
            lock (queues)
                queues.TryGetValue(queue.ToInt64(), out q);
            return q;
        }

        public static nn.Result InitializeQueue(IntPtr queue, Uid uid)
        {
            Queue q = GetQueue(queue);
            if (q != null)
                q.User = uid;
            return new nn.Result();
        }

        public static void TerminateQueue(IntPtr queue)
        {
            Queue q = GetQueue(queue);
            if (q != null)
                q.User = Uid.Invalid;
        }

        public static Uid GetQueueUid(IntPtr queue)
        {
            Queue q = GetQueue(queue);
            return q != null ? q.User : Uid.Invalid;
        }

        // ---- applets ----

        public static nn.Result ShowApplet(string name)
        {
            PcConfig.Log("friends: applet " + name + " (closed)");
            return new nn.Result();
        }
    }
}
#endif
