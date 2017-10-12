#define UsingBotwinderSecure

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
//using Botwinder.modules; //todo - doesn't exist yet...
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
			(new Client()).RunAndWait().GetAwaiter().GetResult();
		}
	}

	class Client
	{
		private BotwinderClient Bot;


		public Client()
		{}

		public async Task RunAndWait()
		{
			while( true )
			{
				try
				{
					this.Bot = new BotwinderClient();
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
	}
}
