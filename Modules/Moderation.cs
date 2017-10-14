using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Botwinder.secure
{
	public class Moderation: IModule
	{
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		private const string BanPmString = "Hello!\nI regret to inform you, that you have been **banned {0} on the {1} server** for the following reason:\n{2}";
		private const string BanNotFoundString = "I couldn't find them :(";
		private const string BanConfirmString = "_\\*fires them railguns at <@{0}>*_  Ò_Ó";
		private const string UnbanConfirmString = "I've unbanned {0}... ó_ò";
		private const string KickArgsString = "I'm supposed to shoot... who?\n";
		private const string KickNotFoundString = "I couldn't find them :(";
		private const string KickPmString = "Hello!\nYou have been kicked out of the **{0} server** by its Moderators for the following reason:\n{1}";
		private const string WarningNotFoundString = "I couldn't find them :(";


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public async Task<List<Command>> Init(IBotwinderClient iClient)
		{
			BotwinderClient client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !op
			Command newCommand = new Command("op");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "_op_ yourself to be able to use `mute`, `kick` or `ban` commands. (Only if configured at <http://botwinder.info/config>)";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				SocketRole role = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( role == null )
				{
					await iClient.SendMessageToChannel(e.Channel, string.Format("I'm really sorry, buuut `{0}op` feature is not configured! Poke your admin to set it up at <http://botwinder.info/config>", e.Server.Config.CommandPrefix));
					return;
				}
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await iClient.SendMessageToChannel(e.Channel, "I don't have `ManageRoles` permission.");
					return;
				}

				string response = "";
				try
				{
					SocketGuildUser user = e.Message.Author as SocketGuildUser;
					if( user.Roles.Any(r => r.Id == e.Server.Config.OperatorRoleId) )
					{
						await user.RemoveRoleAsync(role);
						response = "All done?";
					}
					else
					{
						await user.AddRoleAsync(role);
						response = "Go get em tiger!";
					}
				}
				catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null && ex.HttpCode == System.Net.HttpStatusCode.Forbidden )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: Botwinder has to be above other roles to be able to manage them: <http://i.imgur.com/T8MPvME.png>)";
					else
						throw;
				}
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !mute
			newCommand = new Command("mute");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Temporarily mute mentioned members from both the chat and voice. This command has to be configured at <http://botwinder.info/config>!";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}
				catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null && ex.HttpCode == System.Net.HttpStatusCode.Forbidden )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: Botwinder has to be above other roles to be able to manage them: <http://i.imgur.com/T8MPvME.png>)";
					else
						throw;
				}
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !munute
			newCommand = new Command("unmute");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Unmute previously muted members. This command has to be configured at <http://botwinder.info/config>.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}
				catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null && ex.HttpCode == System.Net.HttpStatusCode.Forbidden )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: Botwinder has to be above other roles to be able to manage them: <http://i.imgur.com/T8MPvME.png>)";
					else
						throw;
				}
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !kick
			newCommand = new Command("kick");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameters `@user reason` where `@user` = user mention or id; `reason` = worded description why did you kick them out - they will receive this via PM.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.KickMembers )
				{
					await e.Message.Channel.SendMessageSafe(ErrorPermissionsString);
					return;
				}

				SocketRole role = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( role != null && (e.Message.Author as SocketGuildUser).Roles.All(r => r.Id != role.Id) &&
				    !client.IsGlobalAdmin(e.Message.Author.Id) )
				{
					await e.Message.Channel.SendMessageSafe($"`{e.Server.Config.CommandPrefix}op`?");
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, KickArgsString + e.Command.Description);
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, KickNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length + 1 )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					dbContext.Dispose();
					return;
				}

				StringBuilder warning = new StringBuilder();
				for(int i = mentionedUsers.Count; i < e.MessageArgs.Length; i++)
				{
					warning.Append(e.MessageArgs[i]);
					warning.Append(" ");
				}

				string response = "";
				List<string> usernames = new List<string>();
				try
				{
					foreach( UserData userData in mentionedUsers )
					{
						SocketGuildUser user = e.Server.Guild.GetUser(userData.UserId);
						if( user == null )
							continue;

						try
						{
							await user.SendMessageSafe(string.Format(KickPmString,
								e.Server.Guild.Name, warning.ToString()));
						}
						catch(Exception) { }
						await Task.Delay(300);

						await user.KickAsync(warning.ToString());
						userData.AddWarning(warning.ToString());
						usernames.Add(user.GetUsername());
					}
				}
				catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null && ex.HttpCode == System.Net.HttpStatusCode.Forbidden )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: Botwinder has to be above other roles to be able to manage them: <http://i.imgur.com/T8MPvME.png>)";
					else
						throw;
				}

				if( !usernames.Any() )
				{
					response = "I wasn't able to shoot anyone.";
				}
				else if( string.IsNullOrEmpty(response) )
				{
					response = "I've fired them railguns at " + usernames.ToString() + ".";
					dbContext.SaveChanges();
				}

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !ban
			newCommand = new Command("ban");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameters `@user time reason` where `@user` = user mention or id; `time` = duration of the ban (e.g. `7d` or `12h` or `0` for permanent.); `reason` = worded description why did you ban them - they will receive this via PM (use `silentBan` to not send the PM)";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				if( e.MessageArgs == null || e.MessageArgs.Length < 3 )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				if( !e.Server.Guild.CurrentUser.GuildPermissions.BanMembers )
				{
					await e.Message.Channel.SendMessageSafe(ErrorPermissionsString);
					return;
				}

				SocketRole role = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( role != null && (e.Message.Author as SocketGuildUser).Roles.All(r => r.Id != role.Id) &&
				    !client.IsGlobalAdmin(e.Message.Author.Id) )
				{
					await e.Message.Channel.SendMessageSafe($"`{e.Server.Config.CommandPrefix}op`?");
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, BanNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length + 2 )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					dbContext.Dispose();
					return;
				}

				StringBuilder warning = new StringBuilder();
				for(int i = mentionedUsers.Count + 1; i < e.MessageArgs.Length; i++)
				{
					warning.Append(e.MessageArgs[i]);
					warning.Append(" ");
				}

				int banDurationHours = 0;
				Match dayMatch = Regex.Match(e.MessageArgs[mentionedUsers.Count], "\\d+d", RegexOptions.IgnoreCase);
				Match hourMatch = Regex.Match(e.MessageArgs[mentionedUsers.Count], "\\d+h", RegexOptions.IgnoreCase);

				if( !hourMatch.Success && !dayMatch.Success && !int.TryParse(e.MessageArgs[mentionedUsers.Count], out banDurationHours) )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					dbContext.Dispose();
					return;
				}

				if( hourMatch.Success )
					banDurationHours = int.Parse(hourMatch.Value.Trim('h').Trim('H'));
				if( dayMatch.Success )
					banDurationHours += 24 * int.Parse(dayMatch.Value.Trim('d').Trim('D'));

				string response = "ò_ó";

				try
				{
					response = await Ban(e.Server, mentionedUsers, TimeSpan.FromHours(banDurationHours),
						e.Server.Config.QuickbanReason, e.Message.Author as SocketGuildUser,
						e.Command.Id.ToLower() == "silentban", e.Command.Id.ToLower() == "purgeban");
					dbContext.SaveChanges();
				} catch(Exception exception)
				{
					await client.LogException(exception, e);
					response = $"Unknown error, please poke <@{client.GlobalConfig.AdminUserId}> to take a look x_x";
				}

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("silentban");
			newCommand.Description = "Use with the same parameters like `ban`. The _reason_ message will not be sent to the user (hence silent.)";
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("purgeban");
			newCommand.Description = "Use with the same parameters like `ban`. The difference is that this command will also delete all the messages of the user in last 24 hours.";
			commands.Add(newCommand);

// !quickban
			newCommand = new Command("quickban");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Quickly ban someone using pre-configured reason and duration, it also removes their messages. You can mention several people at once. (This command has to be first configured via `config` or <http://botwinder.info/config>.)";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				guid id;
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, BanNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					dbContext.Dispose();
					return;
				}

				string response = "ò_ó";

				try
				{
					response = await Ban(e.Server, mentionedUsers, TimeSpan.FromHours(e.Server.Config.QuickbanDuration), e.Server.Config.QuickbanReason, e.Message.Author as SocketGuildUser);
					dbContext.SaveChanges();
				} catch(Exception exception)
				{
					await client.LogException(exception, e);
					response = $"Unknown error, please poke <@{client.GlobalConfig.AdminUserId}> to take a look x_x";
				}

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !unban
			newCommand = new Command("unban");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameter `@user` where `@user` = user mention or id;";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				guid id;
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, BanNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					dbContext.Dispose();
					return;
				}

				string response = "ó_ò";

				try
				{
					response = await UnBan(e.Server, mentionedUsers);
					dbContext.SaveChanges();
				} catch(Exception exception)
				{
					await client.LogException(exception, e);
					response = $"Unknown error, please poke <@{client.GlobalConfig.AdminUserId}> to take a look x_x";
				}

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !addWarning
			newCommand = new Command("addWarning");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameters `@user warning` where `@user` = user mention or id, you can add the same warning to multiple people, just mention them all; `warning` = worded description, a warning message to store in the database.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, "Give a warning to whom?\n" + e.Command.Description);
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, WarningNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length + 1 )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					dbContext.Dispose();
					return;
				}

				StringBuilder warning = new StringBuilder();
				for(int i = mentionedUsers.Count; i < e.MessageArgs.Length; i++)
				{
					warning.Append(e.MessageArgs[i]);
					warning.Append(" ");
				}

				bool sendMessage = e.Command.Id.ToLower() == "issuewarning";
				foreach(UserData userData in mentionedUsers)
				{
					userData.AddWarning(warning.ToString());
					if( sendMessage )
					{
						try
						{
							SocketGuildUser user = e.Server.Guild.GetUser(userData.UserId);
							if( user != null )
								await user.SendMessageSafe(string.Format("Hello!\nYou have been issued a formal **warning** by the Moderators of the **{0} server** for the following reason:\n{1}",
									e.Server.Guild.Name, warning.ToString()));
						}
						catch(Exception) { }
					}
				}

				dbContext.SaveChanges();

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, "Done.");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("addwarning"));

			newCommand = newCommand.CreateCopy("issueWarning");
			newCommand.Description = "Use with parameters `@user warning` where `@user` = user mention or id, you can add the same warning to multiple people, just mention them all; `warning` = worded description, a warning message to store in the database. This will also be PMed to the user.";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("issuewarning"));

// !removeWarning
			newCommand = new Command("removeWarning");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameter `@user` = user mention or id, you can remove the last warning from multiple people, just mention them all.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, "Remove warning from whom?\n" + e.Command.Description);
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, WarningNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.\n" + e.Command.Description);
					dbContext.Dispose();
					return;
				}

				bool allWarnings = e.Command.Id.ToLower() == "removeallwarnings";

				bool modified = false;
				foreach(UserData userData in mentionedUsers)
				{
					if( userData.WarningCount == 0 )
						continue;

					modified = true;
					if( allWarnings )
					{
						userData.WarningCount = 0;
						userData.Notes = "";
					}
					else
					{
						userData.WarningCount--;
						if( userData.Notes.Contains(" | ") )
						{
							int index = userData.Notes.LastIndexOf(" |");
							userData.Notes = userData.Notes.Remove(index);
						}
						else
						{
							userData.Notes = "";
						}
					}
				}

				if( modified )
					dbContext.SaveChanges();

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, "Done.");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removewarning"));

			newCommand = newCommand.CreateCopy("removeAllWarnings");
			newCommand.Description = "Use with parameter `@user` = user mention or id, you can remove all the warnings from multiple people, just mention them all.";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removeallwarnings"));

// !whois
			newCommand = new Command("whois");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Search for a User on this server.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, "Who are you looking for?");
					return;
				}

				string expression = e.TrimmedMessage.ToLower();
				List<guid> foundUserIds = e.Server.Guild.Users
					.Where(u => u.Username.ToLower().Contains(expression) ||
					            (u.Nickname != null && u.Nickname.Contains(expression)))
					.Select(u => u.Id).ToList();

				foundUserIds.AddRange(e.Message.MentionedUsers.Select(u => u.Id));

				for( int i = 0; i < e.MessageArgs.Length; i++ )
				{
					if( guid.TryParse(e.MessageArgs[i], out guid id) )
						foundUserIds.Add(id);
				}

				string response = "I found too many, please be more specific.";
				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());

				if( foundUserIds.Count <= 5 )
				{
					StringBuilder whoisStrings = new StringBuilder();
					for( int i = 0; i < foundUserIds.Count; i++ )
					{
						UserData userData = dbContext.UserDatabase.FirstOrDefault(u => u.ServerId == e.Server.Id && u.UserId == foundUserIds[i]);
						if( userData != null )
							whoisStrings.AppendLine(userData.GetWhoisString(dbContext));
					}

					if( string.IsNullOrEmpty(response) )
					{
						response = "I did not find anyone.";
					}

					response = whoisStrings.ToString();
				}

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !find
			newCommand = new Command("find");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Find a User in the database."; //todo - improve desc.
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, "Who are you looking for?");
					return;
				}

				string response = "I found too many, please be more specific.";
				string expression = e.TrimmedMessage.ToLower();
				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<Username> foundUsernames = dbContext.Usernames.Where(u => u.ServerId == e.Server.Id && u.Name.ToLower().Contains(expression)).ToList();
				List<Nickname> foundNicknames = dbContext.Nicknames.Where(u => u.ServerId == e.Server.Id && u.Name.ToLower().Contains(expression)).ToList();
				List<guid> foundUserIds = new List<guid>();

				for( int i = 0; i < e.MessageArgs.Length; i++ )
				{
					if( guid.TryParse(e.MessageArgs[i], out guid id) )
						foundUserIds.Add(id);
				}

				foreach( Username username in foundUsernames )
				{
					if( foundUserIds.Contains(username.UserId) )
						continue;

					foundUserIds.Add(username.UserId);
				}
				foreach( Nickname nickname in foundNicknames )
				{
					if( foundUserIds.Contains(nickname.UserId) )
						continue;

					foundUserIds.Add(nickname.UserId);
				}

				if( foundUserIds.Count <= 5 )
				{
					StringBuilder whoisStrings = new StringBuilder();
					for( int i = 0; i < foundUserIds.Count; i++ )
					{
						UserData userData = dbContext.UserDatabase.FirstOrDefault(u => u.ServerId == e.Server.Id && u.UserId == foundUserIds[i]);
						if( userData != null )
							whoisStrings.AppendLine(userData.GetWhoisString(dbContext));
					}

					response = whoisStrings.ToString();
				}

				if( string.IsNullOrEmpty(response) )
				{
					response = "I did not find anyone.";
				}

				dbContext.Dispose();
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

			return commands;
		}

		public Task Update(IBotwinderClient iClient)
		{
			BotwinderClient client = iClient as BotwinderClient;
			throw new NotImplementedException();
		}




		/// <summary> Ban the User - this will also ban them as soon as they join the server, if they are not there right now. </summary>
		/// <param name="duration">Use zero for permanent ban.</param>
		/// <param name="silent">Set to true to not PM the user information about the ban (time, server, reason)</param>
		public async Task<string> Ban(Server server, List<UserData> users, TimeSpan duration, string reason, SocketGuildUser bannedBy = null, bool silent = false, bool deleteMessages = false)
		{
			DateTime bannedUntil = DateTime.MaxValue;
			if( duration.TotalHours >= 1 )
				bannedUntil = DateTime.UtcNow + duration;
			else
				duration = TimeSpan.Zero;

			StringBuilder durationString = new StringBuilder();
			if( duration == TimeSpan.Zero )
				durationString.Append("permanently");
			else
			{
				durationString.Append("for ");
				if( duration.Days > 0 )
				{
					durationString.Append(duration.Days);
					durationString.Append(duration.Days == 1 ? " day" : " days");
					if( duration.Hours > 0 )
						durationString.Append(" and ");
				}
				if( duration.Hours > 0 )
				{
					durationString.Append(duration.Hours);
					durationString.Append(duration.Hours == 1 ? " hour" : " hours");
				}
			}

			string response = "";
			List<guid> banned = new List<guid>();
			foreach( UserData userData in users )
			{
				SocketGuildUser user;
				if( !silent && (user = server.Guild.GetUser(userData.UserId)) != null )
				{
					try
					{
						await user.SendMessageSafe(string.Format(BanPmString,
							durationString.ToString(), server.Guild.Name, reason));
					}
					catch(Exception) { }
				}

				await Task.Delay(500);

				try
				{
					//this.RecentlyBannedUserIDs.Add(user.Id); //Don't trigger the on-event log message as well as this custom one.

					await server.Guild.AddBanAsync(userData.UserId, (deleteMessages ? 1 : 0), reason);
					userData.BannedUntil = bannedUntil;
					userData.AddWarning($"Banned {durationString.ToString()} with reason: {reason}");
					banned.Add(userData.UserId);

					//client.Events.UserBanned(user, s.DiscordServer, bannedUntil, reason, bannedBy: bannedBy); //todo - log channel
				}
				catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null && ex.HttpCode == System.Net.HttpStatusCode.Forbidden )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: Botwinder has to be above other roles to be able to manage them: <http://i.imgur.com/T8MPvME.png>)";
					else
						throw;
				}
			}

			if( banned.Any() )
				response = string.Format(BanConfirmString, banned.ToString());

			return response;
		}

		public async Task<string> UnBan(Server server, List<UserData> users)
		{
			string response = "";
			List<guid> unbanned = new List<guid>();
			foreach( UserData user in users )
			{
				try
				{
					//this.RecentlyBannedUserIDs.Add(user.Id); //Don't trigger the on-event log message as well as this custom one.

					await server.Guild.RemoveBanAsync(user.UserId);

					//UserUnbanned(user, s.DiscordServer, bannedUntil, reason, bannedBy: bannedBy); //todo - log channel

					user.BannedUntil = DateTime.MinValue;
					unbanned.Add(user.UserId);
				}
				catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null && ex.HttpCode == System.Net.HttpStatusCode.Forbidden )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: Botwinder has to be above other roles to be able to manage them: <http://i.imgur.com/T8MPvME.png>)";
					else
						throw;
				}
			}

			if( unbanned.Any() )
				response = string.Format(UnbanConfirmString, unbanned.ToString());

			return response;
		}
	}
}
