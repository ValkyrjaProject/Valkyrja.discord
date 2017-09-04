using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using guid = System.Int64;

namespace Botwinder.entities
{
	public class Server<TUser> where TUser: UserData, new()
	{
		public readonly guid Id;
		public readonly Dictionary<string, Command<TUser>> Commands;
		public Dictionary<string, CustomCommand> CustomCommands;
		public Dictionary<string, CustomAlias> CustomAliases;
		public Dictionary<string, CommandOptions> CommandOptions;
		public Dictionary<string, CommandChannelOptions> CommandChannelOptions;
		public ServerConfig Config;

		public Server(guid id, Dictionary<string, Command<TUser>> allCommands, ServerContext db)
		{
			this.Id = id;
			this.Commands = new Dictionary<string, Command<TUser>>(allCommands);
			ReloadConfig(db);
		}

		public void ReloadConfig(ServerContext db)
		{
			this.Config = db.ServerConfigurations.FirstOrDefault(c => c.ServerId == this.Id);
			if( this.Config == null )
			{
				this.Config = new ServerConfig(); //todo actually create that config properly...
				db.ServerConfigurations.Add(this.Config);
				db.SaveChanges();
			}

			this.CustomCommands.Clear();
			this.CustomAliases.Clear();
			this.CommandOptions.Clear();
			this.CommandChannelOptions.Clear();

			this.CustomCommands = db.CustomCommands.Where(c => c.ServerId == this.Id).ToDictionary(c => c.CommandId);
			this.CustomAliases = db.CustomAliases.Where(c => c.ServerId == this.Id).ToDictionary(c => c.Alias);
			this.CommandOptions = db.CommandOptions.Where(c => c.ServerId == this.Id).ToDictionary(c => c.CommandId);
			this.CommandChannelOptions = db.CommandChannelOptions.Where(c => c.ServerId == this.Id).ToDictionary(c => c.CommandId);
		}
	}
}
