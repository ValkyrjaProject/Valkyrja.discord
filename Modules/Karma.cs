using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Net;
using Valkyrja.core;
using Valkyrja.entities;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Valkyrja.modules
{
	public class Karma: IModule
	{
		private const string KarmaDisabledString = "Karma is disabled on this server.";

		private ValkyrjaClient Client;
		private readonly Regex RegexKarma = new Regex(".*(?<!\\\\)(thank(?!sgiving)|thx|ʞuɐɥʇ|danke|vielen dank|gracias|merci(?!al)|grazie|arigato|dziękuję|dziekuje|obrigad).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);


		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.MessageReceived += OnMessageReceived;

// !top
			Command newCommand = new Command("top");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Check how many cookies you've got.";
			newCommand.ManPage = new ManPage("[n]", "`[n]` - Optional argument specifying how many members with the highest count you would like to fetch. Default 5.");
			newCommand.IsPremiumServerwideCommand = true;
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.KarmaEnabled )
				{
					await e.SendReplySafe(KarmaDisabledString);
					return;
				}

				const int maxUsers = 200;
				int n = 5;
				if( e.MessageArgs != null && e.MessageArgs.Length > 0 && !int.TryParse(e.MessageArgs[0], out n) )
				{
					await e.SendReplySafe("Invalid argument.");
					return;
				}

				if( n > 20 )
					n = 20;

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				IEnumerable<UserData> userData = dbContext.UserDatabase.Where(u => u.ServerId == e.Server.Id && u.KarmaCount > 0).OrderByDescending(u => u.KarmaCount).Take(maxUsers).SkipWhile(u => e.Server.Guild.GetUser(u.UserId) == null).Take(n);

				int i = 1;
				StringBuilder response = new StringBuilder($"Here is the top {n} {e.Server.Config.KarmaCurrencySingular} holders:");
				foreach( UserData user in userData )
				{
					response.AppendLine($"**{i++})** {e.Server.Guild.GetUser(user.UserId)?.GetNickname().Replace("@everyone", "@-everyone").Replace("@here", "@-here")} : `{user.KarmaCount}`");
				}

				await e.SendReplySafe(response.ToString());

				dbContext.Dispose();
			};
			commands.Add(newCommand);

// !cookies
			newCommand = new Command("cookies");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Check how many cookies you've got.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.IsPremiumServerwideCommand = true;
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.KarmaEnabled )
				{
					await e.SendReplySafe(KarmaDisabledString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);

				int articleIndex = e.Server.Config.KarmaCurrencySingular[0] == ':' ? 1 : 0;
				string article = e.Server.Config.KarmaCurrencySingular[articleIndex] == 'a' ? "an" : "a";
				await e.SendReplySafe(string.Format("Hai **{0}**, you have {1} {2}!\nYou can {4} one with the `{3}{4}` command, or you can give {5} {6} to your friend using `{3}give @friend`",
					e.Message.Author.GetNickname(), userData.KarmaCount,
					(userData.KarmaCount == 1 ? e.Server.Config.KarmaCurrencySingular : e.Server.Config.KarmaCurrency),
					e.Server.Config.CommandPrefix, e.Server.Config.KarmaConsumeCommand,
					article, e.Server.Config.KarmaCurrencySingular));

				dbContext.Dispose();
			};
			commands.Add(newCommand);

// !nom
			newCommand = new Command("nom");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Eat one of your cookies!";
			newCommand.ManPage = new ManPage("", "");
			newCommand.IsPremiumServerwideCommand = true;
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.KarmaEnabled )
				{
					await e.SendReplySafe(KarmaDisabledString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);

				if( userData.KarmaCount <= 0 )
				{
					await e.SendReplySafe(string.Format("Umm... I'm sorry **{0}** you don't have any {1} left =(",
						e.Message.Author.GetNickname(), e.Server.Config.KarmaCurrency));

					dbContext.Dispose();
					return;
				}

				userData.KarmaCount -= 1;
				dbContext.SaveChanges();

				await e.SendReplySafe(string.Format("**{0}** just {1} one of {2} {3}! {4} {5} left.",
					e.Message.Author.GetNickname(), e.Server.Config.KarmaConsumeVerb,
					(this.Client.IsGlobalAdmin(e.Message.Author.Id) ? "her" : "their"), e.Server.Config.KarmaCurrency,
					(this.Client.IsGlobalAdmin(e.Message.Author.Id) ? "She has" : "They have"), userData.KarmaCount));// Because i can :P

				dbContext.Dispose();
			};
			commands.Add(newCommand);

// !give
			newCommand = new Command("give");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Give one of your cookies to a friend =]";
			newCommand.ManPage = new ManPage("<@user>", "`<@user>` - User mention of a user to receive a cookie.");
			newCommand.IsPremiumServerwideCommand = true;
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.KarmaEnabled )
				{
					await e.SendReplySafe(KarmaDisabledString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);
				if( userData.KarmaCount == 0 )
				{
					await e.SendReplySafe($"Umm... I'm sorry **{e.Message.Author.GetNickname()}**, you don't have any {e.Server.Config.KarmaCurrency} left =(");

					dbContext.Dispose();
					return;
				}

				if( e.Message.MentionedUsers == null || !e.Message.MentionedUsers.Any() || e.Message.MentionedUsers.Count() > e.Server.Config.KarmaLimitMentions )
				{
					await e.SendReplySafe($"You have to @mention your friend who will receive the {e.Server.Config.KarmaCurrencySingular}. You can mention up to {e.Server.Config.KarmaLimitMentions} people at the same time.");

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

					userNames.Append((count++ == 0 ? "" : count == users.Count ? ", and " : ", ") + (e.Server.Guild.GetUser(mentionedUser.UserId)?.GetNickname() ?? (await this.Client.DiscordClient.Rest.GetGuildUserAsync(e.Server.Id, mentionedUser.UserId))?.GetNickname() ?? "nobody"));
				}

				if( count > 0 )
					dbContext.SaveChanges();

				int articleIndex = e.Server.Config.KarmaCurrencySingular[0] == ':' ? 1 : 0;
				string article = e.Server.Config.KarmaCurrencySingular[articleIndex] == 'a' ? "an" : "a";
				string response = $"**{userNames}** received {article} {e.Server.Config.KarmaCurrencySingular} of friendship from **{e.Message.Author.GetNickname()}** =]";
				if( count < users.Count )
					response += "\nBut I couldn't give out more, as you don't have any left =(";

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);


			return commands;
		}

		private async Task OnMessageReceived(SocketMessage message)
		{
			if( !this.Client.GlobalConfig.ModuleUpdateEnabled )
				return;

			Server server;
			if( !(message.Channel is SocketTextChannel channel) || !this.Client.Servers.ContainsKey(channel.Guild.Id) || (server = this.Client.Servers[channel.Guild.Id]) == null )
				return;
			if( !(message.Author is SocketGuildUser user) || message.Author.IsBot )
				return;
			if( !this.Client.IsPremium(server) && !this.Client.IsTrialServer(server.Id) )
				return;
			if( !server.Config.KarmaEnabled || message.MentionedUsers == null || !message.MentionedUsers.Any() )
				return;

			bool match = false;
			foreach( string line in message.Content.Split('\n') )
				if( !line.StartsWith(">") && this.RegexKarma.Match(line).Success )
					match = true;
			if( !match )
				return;

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

			try
			{
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
				foreach( UserData mentionedUser in mentionedUserData )
				{
					if( mentionedUser.UserId != user.Id )
					{
						mentionedUser.KarmaCount++;

						userNames.Append((thanked++ == 0 ? "" : thanked == count ? ", and " : ", ") + (server.Guild.GetUser(mentionedUser.UserId)?.GetNickname() ?? (await this.Client.DiscordClient.Rest.GetGuildUserAsync(server.Id, mentionedUser.UserId))?.GetNickname() ?? "Someone"));
					}
				}

				if( thanked > 0 )
				{
					userData.LastThanksTime = DateTime.UtcNow;
					dbContext.SaveChanges();
					if( server.Config.IgnoreEveryone )
						userNames = userNames.Replace("@everyone", "@-everyone").Replace("@here", "@-here");
					await channel.SendMessageSafe($"**{userNames}** received a _thank you_ {server.Config.KarmaCurrencySingular}!");
				}
			}
			catch( HttpException exception )
			{
				await server.HandleHttpException(exception, $"This was a `thank you` karma message in <#{channel.Id}>");
			}

			dbContext.Dispose();
		}

		public Task Update(IValkyrjaClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
