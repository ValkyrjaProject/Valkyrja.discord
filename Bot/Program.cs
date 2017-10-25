#define UsingBotwinderSecure

using System;
using System.Linq.Expressions;
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
	}
}
