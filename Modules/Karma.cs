using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Karma: IModule
	{
		private const string KarmaDisabledString = "Karma is disabled on this server.";

		private BotwinderClient Client;
		private readonly Regex RegexKarma = new Regex(".*(?<!\\\\)(thank(?!sgiving)|thx|ʞuɐɥʇ|danke|vielen dank|gracias|merci(?!al)|grazie|arigato|dziękuję|dziekuje|obrigad).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.MessageReceived += OnMessageReceived;

// !cookies
			Command newCommand = new Command("cookies");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Check how many cookies you've got.";
			newCommand.IsPremiumServerwideCommand = true;
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.KarmaEnabled )
				{
					await this.Client.SendMessageToChannel(e.Channel, KarmaDisabledString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);

				await this.Client.SendMessageToChannel(e.Channel, string.Format("Hai **{0}**, you have {1} {2}!\nYou can {4} one with the `{3}{4}` command, or you can give a {5} to your friend using `{3}give @friend`",
					e.Message.Author.GetNickname(), userData.KarmaCount,
					(userData.KarmaCount == 1 ? e.Server.Config.KarmaCurrencySingular : e.Server.Config.KarmaCurrency),
					e.Server.Config.CommandPrefix, e.Server.Config.KarmaConsumeCommand,
					e.Server.Config.KarmaCurrencySingular));

				dbContext.Dispose();
			};
			commands.Add(newCommand);

// !nom
			newCommand = new Command("nom");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Eat one of your cookies!";
			newCommand.IsPremiumServerwideCommand = true;
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.KarmaEnabled )
				{
					await this.Client.SendMessageToChannel(e.Channel, KarmaDisabledString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);

				if( userData.KarmaCount <= 0 )
				{
					await this.Client.SendMessageToChannel(e.Channel, string.Format("Umm... I'm sorry **{0}** you don't have any {1} left =(",
						e.Message.Author.GetNickname(), e.Server.Config.KarmaCurrency));

					dbContext.Dispose();
					return;
				}

				userData.KarmaCount -= 1;

				await this.Client.SendMessageToChannel(e.Channel, string.Format("**{0}** just {1} one of {2} {3}! {4} {5} left.",
					e.Message.Author.GetNickname(), e.Server.Config.KarmaConsumeVerb,
					(this.Client.IsGlobalAdmin(e.Message.Author.Id) ? "her" : "their"), e.Server.Config.KarmaCurrency,
					(this.Client.IsGlobalAdmin(e.Message.Author.Id) ? "She has" : "They have"), userData.KarmaCount));// Because i can :P

				dbContext.SaveChanges();
				dbContext.Dispose();
			};
			commands.Add(newCommand);

// !give
			newCommand = new Command("give");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Give one of your cookies to a friend =] (use with their @mention as a parameter)";
			newCommand.IsPremiumServerwideCommand = true;
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.KarmaEnabled )
				{
					await this.Client.SendMessageToChannel(e.Channel, KarmaDisabledString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);
				if( userData.KarmaCount == 0 )
				{
					await this.Client.SendMessageToChannel(e.Channel, string.Format("Umm... I'm sorry **{0}**, you don't have any {1} left =(",
						e.Message.Author.GetNickname(), e.Server.Config.KarmaCurrency));

					dbContext.Dispose();
					return;
				}

				if( e.Message.MentionedUsers == null || !e.Message.MentionedUsers.Any() || e.Message.MentionedUsers.Count() > e.Server.Config.KarmaLimitMentions )
				{
					await this.Client.SendMessageToChannel(e.Channel, string.Format("You have to @mention your friend who will receive the {0}. You can mention up to {1} people at the same time.",
						e.Server.Config.KarmaCurrencySingular, e.Server.Config.KarmaLimitMentions));

					dbContext.Dispose();
					return;
				}

				int count = 0;
				StringBuilder userNames = new StringBuilder();
				List<UserData> users = this.Client.GetMentionedUsersData(dbContext, e);
				foreach(UserData mentionedUser in users)
				{
					if( userData.KarmaCount == 0 )
						break;

					userData.KarmaCount--;
					mentionedUser.KarmaCount++;

					userNames.Append((count++ == 0 ? "" : count == users.Count ? ", and " : ", ") + e.Server.Guild.GetUser(mentionedUser.UserId).GetNickname());
				}

				string response = string.Format("**{0}** received a {1} of friendship from **{2}** =]",
					userNames, e.Server.Config.KarmaCurrencySingular, e.Message.Author.GetNickname());
				if( count < users.Count )
					response += "\nBut I couldn't give out more, as you don't have any left =(";
				await this.Client.SendMessageToChannel(e.Channel, response);

				if( count > 0 )
					dbContext.SaveChanges();

				dbContext.Dispose();
			};
			commands.Add(newCommand);


			return commands;
		}

		private async Task OnMessageReceived(SocketMessage message)
		{
			Server server;
			if( message.Author.IsBot ||
			    !(message.Channel is SocketTextChannel channel) ||
			    !this.Client.Servers.ContainsKey(channel.Guild.Id) ||
			    (server = this.Client.Servers[channel.Guild.Id]) == null ||
			    !(message.Author is SocketGuildUser user) ||
			    !server.Config.KarmaEnabled||
			    message.MentionedUsers == null || !message.MentionedUsers.Any() ||
			    !(this.Client.IsPremiumPartner(server.Id) || this.Client.IsPremiumSubscriber(server.Guild.OwnerId)) ||
			    !this.RegexKarma.Match(message.Content).Success )
				return;

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

			UserData userData = dbContext.GetOrAddUser(server.Id, user.Id);
			IEnumerable<UserData> mentionedUserData = message.MentionedUsers.Select(u => dbContext.GetOrAddUser(server.Id, u.Id));
			int count = mentionedUserData.Count();

			if( (count > server.Config.KarmaLimitMentions || userData.LastThanksTime.AddMinutes(server.Config.KarmaLimitMinutes) > DateTimeOffset.UtcNow) )
			{
				if( server.Config.KarmaLimitResponse )
					await message.Channel.SendMessageSafe("You're thanking too much ó_ò");
				return;
			}

			int thanked = 0;
			StringBuilder userNames = new StringBuilder();
			foreach(UserData mentionedUser in mentionedUserData)
			{
				if( mentionedUser.UserId != user.Id )
				{
					mentionedUser.KarmaCount++;

					userNames.Append((thanked++ == 0 ? "" : thanked == count ? ", and " : ", ") + server.Guild.GetUser(mentionedUser.UserId).GetNickname());
				}
			}

			if( thanked > 0 )
			{
				userData.LastThanksTime = DateTime.UtcNow;
				dbContext.SaveChanges();
				await this.Client.SendMessageToChannel(channel, string.Format("**{0}** received a _thank you_ {1}!", userNames, server.Config.KarmaCurrencySingular));
			}

			dbContext.Dispose();
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
