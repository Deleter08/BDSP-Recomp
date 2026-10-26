#if !SWITCH
// PC platform layer: nn::account with one user (PcConfig), as a console whose launcher pre-selected that user (BDSP requires a
// startup user, so TryOpenPreselectedUser succeeds on the console). NOT shipped code.
using nn.account;

namespace PcPlatform
{
    public static class PcAccount
    {
        const int Module = 124;
        public static readonly nn.Result UserNotExist = new nn.Result(Module, 100);

        static bool opened;

        public static Uid User
        {
            get { return new Uid { _data0 = PcConfig.UserId0, _data1 = PcConfig.UserId1 }; }
        }

        public static UserHandle Handle
        {
            get { return new UserHandle { _data0 = PcConfig.UserId0, _data1 = PcConfig.UserId1, _context = 1 }; }
        }

        public static void Initialize()
        {
            PcConfig.Log("account: user " + User.ToString() + " \"" + PcConfig.Nickname + "\"");
        }

        public static nn.Result GetUserCount(ref int count)
        {
            count = 1;
            return PcFs.Success;
        }

        public static nn.Result GetUserExistence(ref bool exists, Uid user)
        {
            exists = user == User;
            return PcFs.Success;
        }

        public static nn.Result ListUsers(ref int length, Uid[] users, int arrayLength, bool openOnly)
        {
            length = 0;
            if (openOnly && !opened)
                return PcFs.Success;
            if (arrayLength > 0 && users != null && users.Length > 0)
            {
                users[0] = User;
                length = 1;
            }
            return PcFs.Success;
        }

        public static nn.Result GetLastOpenedUser(ref Uid user)
        {
            user = User;
            return PcFs.Success;
        }

        public static nn.Result GetNickname(ref Nickname nickname, Uid user)
        {
            if (user != User)
                return UserNotExist;
            nickname.name = PcConfig.Nickname;
            return PcFs.Success;
        }

        public static nn.Result OpenUser(ref UserHandle handle, Uid user)
        {
            if (user != User)
                return UserNotExist;
            handle = Handle;
            opened = true;
            return PcFs.Success;
        }

        public static bool TryOpenPreselectedUser(ref UserHandle handle)
        {
            handle = Handle;
            opened = true;
            return true;
        }

        public static void CloseUser(UserHandle handle)
        {
            opened = false;
        }

        public static nn.Result GetUserId(ref Uid user, UserHandle handle)
        {
            user = new Uid { _data0 = handle._data0, _data1 = handle._data1 };
            return PcFs.Success;
        }

        // The user-selector applet: the only user is picked.
        public static nn.Result ShowUserSelector(ref Uid user)
        {
            user = User;
            return PcFs.Success;
        }
    }
}
#endif
