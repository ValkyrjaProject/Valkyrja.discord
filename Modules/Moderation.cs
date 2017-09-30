using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Botwinder.secure
{
	public class Antispam: IModule
	{
		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public async Task<List<Command<TUser>>> Init<TUser>(IBotwinderClient<TUser> iClient) where TUser : UserData, new()
		{
			BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
			List<Command<TUser>> commands = new List<Command<TUser>>();

// !op
			Command<TUser> newCommand = new Command<TUser>("op");
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
				}catch(Exception exception)
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
			newCommand = new Command<TUser>("mute");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Temporarily mute mentioned members from both the chat and voice. This command has to be configured at <http://botwinder.info/config>!";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}catch(Exception exception)
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
			newCommand = new Command<TUser>("unmute");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Unmute previously muted members. This command has to be configured at <http://botwinder.info/config>.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}catch(Exception exception)
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
			newCommand = new Command<TUser>("kick");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameters `@user reason` where `@user` = user mention or id; `reason` = worded description why did you kick them out - they will receive this via PM.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}catch(Exception exception)
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

// !ban
			newCommand = new Command<TUser>("ban");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameters `@user time reason` where `@user` = user mention or id; `time` = duration of the ban (e.g. `7d` or `12h` or `0` for permanent.); `reason` = worded description why did you ban them - they will receive this via PM (use `silentBan` to not send the PM)";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}catch(Exception exception)
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

			newCommand = newCommand.CreateCopy("silentban");
			newCommand.Description = "Use with the same parameters like `ban`. The _reason_ message will not be sent to the user (hence silent.)";
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("purgeban");
			newCommand.Description = "Use with the same parameters like `ban`. The difference is that this command will also delete all the messages of the user in last 24 hours.";
			commands.Add(newCommand);

// !quickban
			newCommand = new Command<TUser>("quickban");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Quickly ban someone using pre-configured reason and duration, it also removes their messages. You can mention several people at once. (This command has to be first configured via `config` or <http://botwinder.info/config>.)";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}catch(Exception exception)
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

// !unban
			newCommand = new Command<TUser>("unban");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameter `@user` where `@user` = user mention or id;";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				throw new NotImplementedException();
				string response = "";
				try
				{
				}catch(Exception exception)
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

// !addWarning
			newCommand = new Command<TUser>("addWarning");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameters `@user warning` where `@user` = user mention or id, you can add the same warning to multiple people, just mention them all; `warning` = worded description, a warning message to store in the database.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, "Remove warning from who?\n" + e.Command.Description);
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, "I couldn't find them :(");
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
					userData.WarningCount++;
					userData.Notes += string.IsNullOrEmpty(userData.Notes) ? warning.ToString() : (" | " + warning.ToString());
					try
					{
						if( sendMessage )
						{
							SocketGuildUser user = e.Server.Guild.GetUser(userData.UserId);
							if( user != null )
								await user.SendMessageSafe(string.Format("Hello!\nYou have been issued a formal **warning** by the Moderators of the **{0} server** for the following reason:\n{1}",
									e.Server.Guild.Name, warning));
						}
					}
					catch(Exception) { }
				}

				dbContext.SaveChanges();

				await iClient.SendMessageToChannel(e.Channel, "Done.");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("addwarning"));

			newCommand = newCommand.CreateCopy("issueWarning");
			newCommand.Description = "Use with parameters `@user warning` where `@user` = user mention or id, you can add the same warning to multiple people, just mention them all; `warning` = worded description, a warning message to store in the database. This will also be PMed to the user.";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("issuewarning"));

// !removeWarning
			newCommand = new Command<TUser>("removeWarning");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameter `@user` = user mention or id, you can remove the last warning from multiple people, just mention them all.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, "Remove warning from who?\n" + e.Command.Description);
					return;
				}

				ServerContext dbContext = ServerContext.Create(client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, "I couldn't find them :(");
					return;
				}

				bool modified = false;
				foreach(UserData userData in mentionedUsers)
				{
					if( userData.WarningCount == 0 )
						continue;

					modified = true;
					if( e.Command.Id.ToLower() == "removeallwarnings" )
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

				await iClient.SendMessageToChannel(e.Channel, "Done.");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removewarning"));

			newCommand = newCommand.CreateCopy("removeAllWarnings");
			newCommand.Description = "Use with parameter `@user` = user mention or id, you can remove all the warnings from multiple people, just mention them all.";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removeallwarnings"));

// !whois
			newCommand = new Command<TUser>("whois");
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

				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !find
			newCommand = new Command<TUser>("find");
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

				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

			return commands;
		}

		public Task Update<TUser>(IBotwinderClient<TUser> iClient) where TUser : UserData, new()
		{
			BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
			throw new NotImplementedException();
		}
	}
}
