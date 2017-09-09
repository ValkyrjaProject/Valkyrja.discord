#define UsingBotwinderSecure

using System;
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
		private BotwinderClient<UserData> Bot;


		public Client()
		{}

		public async Task RunAndWait()
		{
			this.Bot = new BotwinderClient<UserData>();

			await this.Bot.Connect();

			await Task.Delay(-1);
		}
	}
}
