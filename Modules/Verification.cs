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

        public async Task<List<Command<TUser>>> Init<TUser>(IBotwinderClient<TUser> iClient)
            where TUser : UserData, new()
        {
            BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
            List<Command<TUser>> commands = new List<Command<TUser>>();

            return commands;
        }

        public Task Update<TUser>(IBotwinderClient<TUser> iClient) where TUser : UserData, new()
        {
            BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
            throw new NotImplementedException();
        }
    }
}
