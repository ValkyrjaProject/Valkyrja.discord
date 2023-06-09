using System;
using System.IO;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Valkyrja.modules;

using guid = System.UInt64;

namespace Valkyrja.discord
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
		private ValkyrjaClient Bot;

		private const string BunnehDataFolder = "bunneh";


		public Client()
		{}

		public async Task RunAndWait(int shardIdOverride = - 1)
		{
			try
			{
				while( true )
				{
					this.Bot = new ValkyrjaClient(shardIdOverride);
					InitModules();

						await this.Bot.Connect();
						this.Bot.Events.Initialize += InitCommands;
						await Task.Delay(-1);
				}
			}
			catch(Exception e)
			{
				await this.Bot.LogException(e, "--ValkyrjaClient crashed.");
				this.Bot.Dispose();
			}
		}

		private void InitModules()
		{
			#if VALKYRJASECURE
			this.Bot.Modules.Add(new Valkyrja.secure.Antispam());
			#endif

			this.Bot.Modules.Add(new Moderation());
			this.Bot.Modules.Add(new Verification());
			this.Bot.Modules.Add(new RoleAssignment());
			this.Bot.Modules.Add(new Logging());
			this.Bot.Modules.Add(new Administration());
			this.Bot.Modules.Add(new ExtraFeatures());
			this.Bot.Modules.Add(new Experience());
			this.Bot.Modules.Add(new Karma());
			this.Bot.Modules.Add(new Memo());
			this.Bot.Modules.Add(new Quotes());

			#if VALKYRJASPECIFIC
			this.Bot.Modules.Add(new Recruitment());
			this.Bot.Modules.Add(new MessageFilter());
			#endif
		}

		private Task InitCommands()
		{
// !wat
			Command newCommand = new Command("wat");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "The best command of all time.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				await e.SendReplySafe("**-wat-**\n<http://destroyallsoftware.com/talks/wat>");
			};
			this.Bot.Commands.Add(newCommand.Id.ToLower(), newCommand);

// !bunneh
			newCommand = new Command("bunneh");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "The best command of all time.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( Directory.Exists(GlobalConfig.DataFolder) && Directory.Exists(Path.Combine(GlobalConfig.DataFolder, BunnehDataFolder)) )
				{
					Regex validExtensions = new Regex(".*(jpg|png|gif|mp4).*");
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
			this.Bot.Commands.Add(newCommand.Id.ToLower(), newCommand);

			return Task.CompletedTask;
		}
	}
}
