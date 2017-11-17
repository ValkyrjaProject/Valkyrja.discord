using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;

using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Logging: IModule
	{
		private BotwinderClient Client;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;

			this.Client.Events.UserJoined += OnUserJoined;
			this.Client.Events.UserLeft += OnUserLeft;
			this.Client.Events.UserVoiceStateUpdated += OnUserVoice;
			this.Client.Events.MessageDeleted += OnMessageDeleted;
			this.Client.Events.MessageUpdated += OnMessageUpdated;

			return new List<Command>();
		}

		private async Task OnUserJoined(SocketGuildUser user)
		{
			throw new NotImplementedException();
		}

		private async Task OnUserLeft(SocketGuildUser user)
		{
			throw new NotImplementedException();
		}

		private async Task OnUserVoice(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
		{
			throw new NotImplementedException();
		}

		private async Task OnMessageDeleted(SocketMessage message, ISocketMessageChannel c)
		{
			if( !(c is SocketTextChannel channel) )
				return;

			Server server;
			if( !this.Client.Servers.ContainsKey(channel.Guild.Id) ||
			    (server = this.Client.Servers[channel.Guild.Id]) == null ||
			    server.Config.IgnoreBots && message.Author.IsBot )
				return;

			throw new NotImplementedException();
		}

		private async Task OnMessageUpdated(SocketMessage originalMessage, SocketMessage updatedMessage, ISocketMessageChannel c)
		{
			if( !(c is SocketTextChannel channel) )
				return;

			Server server;
			if( !this.Client.Servers.ContainsKey(channel.Guild.Id) ||
			    (server = this.Client.Servers[channel.Guild.Id]) == null ||
			    server.Config.IgnoreBots && updatedMessage.Author.IsBot )
				return;

			throw new NotImplementedException();
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
