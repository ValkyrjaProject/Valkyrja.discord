using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using guid = System.UInt64;

namespace Botwinder.secure
{
    public class Verification : IModule
    {
        public Func<Exception, string, guid, Task> HandleException{ get; set; }

        public bool UpdateInProgress{ get; set; } = false;

        protected static Verification Instance = null;

        public static Verification Get()
        {
            return Instance;
        }

        private class HashedValue
        {
            public guid UserId;
            public guid ServerId;

            public HashedValue(guid userId, guid serverId)
            {
                this.UserId = userId;
                this.ServerId = serverId;
            }
        }

        public const string VerifySent = "<@{0}>, check your messages!";
        public const string VerifyDonePM = "You have been verified on the `{0}` server =]";
        public const string VerifyDone = "<@{0}> has been verified.";

        public const string VerifyError =
            "If you want me to send a PM with the info to someone, you have to @mention them (only one person though)";

        public const string UserNotFound = "I couldn't find them :(";

        public async Task<List<Command<TUser>>> Init<TUser>(IBotwinderClient<TUser> iClient)
            where TUser : UserData, new()
        {
            BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
            List<Command<TUser>> commands = new List<Command<TUser>>();

//!verify
            Command<TUser> newCommand = new Command<TUser>("verify");
            newCommand.Type = CommandType.Standard;
            newCommand.RequiredPermissions = PermissionType.Everyone;
            newCommand.SendTyping = true;
            newCommand.Description =
                "This will send you some info about verification. You can use this with a parameter to send the info to your friend - you have to @mention them.";
            newCommand.OnExecute += async e =>
            {
                
            };
            commands.Add(newCommand);

            return commands;
        }

        public Task Update<TUser>(IBotwinderClient<TUser> iClient) where TUser : UserData, new()
        {
            BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
            throw new NotImplementedException();
        }
    }
}
