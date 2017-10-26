#define UsingBotwinderSecure

using System;
using System.IO;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Botwinder.modules;
#if UsingBotwinderSecure
using Botwinder.secure;
#endif

using guid = System.UInt64;

namespace Botwinder.discord
{
	class Program
	{
		static void Main(string[] args)
		{
			int shardIdOverride = -1;
			if( args != null && args.Length > 0 && !int.TryParse(args[0], out shardIdOverride) )
			{
				Console.WriteLine("Invalid parameter.");
				return;
			}

			(new Client()).RunAndWait(shardIdOverride).GetAwaiter().GetResult();
		}
	}

	class Client
	{
		private BotwinderClient Bot;

		private const string BunnehDataFolder = "bunneh";


		public Client()
		{}

		public async Task RunAndWait(int shardIdOverride = - 1)
		{
			while( true )
			{
				this.Bot = new BotwinderClient(shardIdOverride);
				this.Bot.Events.Initialize += InitCommands;
				InitModules();

				try
				{
					await this.Bot.Connect();
					await Task.Delay(-1);
				}
				catch(Exception e)
				{
					await this.Bot.LogException(e, "--BotwinderClient crashed.");
					this.Bot.Dispose();
				}
			}
		}

		private void InitModules()
		{
			#if UsingBotwinderSecure
			this.Bot.Modules.Add(new Antispam());
			#endif

			this.Bot.Modules.Add(new Moderation());
			this.Bot.Modules.Add(new Verification());
			this.Bot.Modules.Add(new RoleAssignment());
		}

		private Task InitCommands()
		{
// !wat
			Command newCommand = new Command("wat");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "The best command of all time.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				await this.Bot.SendMessageToChannel(e.Channel, "**-wat-**\n<http://destroyallsoftware.com/talks/wat>");
			};
			this.Bot.Commands.Add(newCommand.Id, newCommand);

			return Task.CompletedTask;
		}
	}
}
