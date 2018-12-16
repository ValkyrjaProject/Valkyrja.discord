#define UsingValkyrjaSecure
#define UsingValkyrjaSpecific

using System;
using System.IO;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Botwinder.modules;
#if UsingValkyrjaSecure
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
				InitModules();

				try
				{
					await this.Bot.Connect();
					this.Bot.Events.Initialize += InitCommands;
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
			#if UsingValkyrjaSecure
			this.Bot.Modules.Add(new Antispam());
			#endif

			this.Bot.Modules.Add(new Moderation());
			this.Bot.Modules.Add(new Verification());
			this.Bot.Modules.Add(new RoleAssignment());
			this.Bot.Modules.Add(new Logging());
			this.Bot.Modules.Add(new Experience());
			this.Bot.Modules.Add(new Karma());
			this.Bot.Modules.Add(new Memo());
			this.Bot.Modules.Add(new Quotes());

			#if UsingValkyrjaSpecific
			this.Bot.Modules.Add(new Recruitment());
			#endif
		}

		private Task InitCommands()
		{
// !wat
			Command newCommand = new Command("wat");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "The best command of all time.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				await e.SendReplySafe("**-wat-**\n<http://destroyallsoftware.com/talks/wat>");
			};
			this.Bot.Commands.Add(newCommand.Id, newCommand);

// !bunneh
			newCommand = new Command("bunneh");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "The best command of all time.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( Directory.Exists(GlobalConfig.DataFolder) && Directory.Exists(Path.Combine(GlobalConfig.DataFolder, BunnehDataFolder)) )
				{
					Regex validExtensions = new Regex(".*(jpg|png|gif).*");
					DirectoryInfo folder = new DirectoryInfo(Path.Combine(GlobalConfig.DataFolder, BunnehDataFolder));
					FileInfo[] files = folder.GetFiles();
					for( int i = 0; files != null && i < 5; i++ )
					{
						int index = Utils.Random.Next(0, files.Length);
						if( validExtensions.Match(files[index].Extension).Success )
						{
							await e.Channel.SendFileAsync(files[index].FullName, "");
							break;
						}
					}
				}
			};
			this.Bot.Commands.Add(newCommand.Id, newCommand);

			return Task.CompletedTask;
		}
	}
}
