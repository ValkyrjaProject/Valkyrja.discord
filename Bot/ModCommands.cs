using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Botwinder.Entities;
using Discord;

using guid = System.UInt64;

namespace Botwinder.Bot
{
	public partial class Commands
	{
		protected static async Task SendBanImage(Channel channel)
		{
			if( Directory.Exists(GlobalConfig.DataFolder) && Directory.Exists(Path.Combine(GlobalConfig.DataFolder, GlobalConfig.BanDataFolder)) )
			{
				Regex validExtensions = new Regex(".*(jpg|png|gif).*");
				DirectoryInfo banFolder = new DirectoryInfo(Path.Combine(GlobalConfig.DataFolder, GlobalConfig.BanDataFolder));
				FileInfo[] files = banFolder.GetFiles();
				for(int i = 0; files != null && i < 5; i++)
				{
					int index = Utils.Random.Next(0, files.Length);
					if( validExtensions.Match(files[index].Extension).Success )
					{
						await channel.SendFile(files[index].FullName);
						break;
					}
				}
			}
		}


		public static List<Command> GetModCommands<TUser>(IBotwinderClient<TUser> client) where TUser : UserData, new()
		{
			List<Command> commands = new List<Command>();

// !stats
			Command newCommand = new Command("stats");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Display some info about this server and some numbers.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin |  Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				int onlineMemberCount = 0;
				int memberCount = 0;
				foreach(User user in e.Message.Server.Users)
				{
					memberCount++;
					if( user.Status == UserStatus.Online || user.Status == UserStatus.Idle )
						onlineMemberCount++;
				}

				TimeSpan uptime = DateTimeOffset.UtcNow - client.TimeStarted;
				int days = uptime.Days;
				int hours = uptime.Hours;
				int minutes = uptime.Minutes;
				int seconds = uptime.Seconds;

				await e.Message.Channel.SendMessage(string.Format("Server name: `{0}`\nServer ID: `{1}`\nOwner: `{2}`\nOwner ID: `{3}`\nMembers Online: `{4}`\nMembers Total: `{5}`\nBotwinder Status: <http://status.botwinder.info>",
					e.Message.Server.Name, e.Message.Server.Id, e.Message.Server.Owner.Name, e.Message.Server.Owner.Id, onlineMemberCount, memberCount ));
			};
			commands.Add(newCommand);

// !membersOf
			newCommand = new Command("membersOf");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Display a list of members of `roleID` or `roleName` parameter.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				guid id = 0;
				Role roleFromId = null;
				List<Role> roles = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || (!guid.TryParse(e.TrimmedMessage, out id) && (roles = e.Message.Server.Roles.Where(r => r.Name.ToLower().Contains(e.TrimmedMessage.ToLower())).ToList()).Count() == 0) )
				{
					await e.Message.Channel.SendMessage("Role not found.");
					return;
				}
				else if( id != 0 && (roleFromId = e.Message.Server.GetRole(id)) != null )
				{
					roles = new List<Role>();
					roles.Add(roleFromId);
				}

				bool useMention = e.Command.ID.ToLower() == "mentionmembersof";
				foreach(Role role in roles)
				{
					string response = string.Format("Members of `{0}`:", role.Name);
					string newString = "";
					foreach(User user in role.Members)
					{
						newString = (useMention ? "\n  <@" + user.Id + "> | `" + user.Id + "`" : "\n  " + user.Name + " | `" + user.Id + "`");
						if( newString.Length + response.Length > GlobalConfig.MessageCharacterLimit )
						{
							await e.Message.Channel.SendMessage(response);
							response = "";
						}

						response += newString;
					}

					await e.Message.Channel.SendMessage(response);
				}
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("membersof"));
			newCommand = newCommand.CreateCopy("mentionMembersOf");
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.Description = "Display a list of members of `roleID` or `roleName` parameter. _(This will mention their names - use in closed channel or use `membersOf`)_";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("mentionmembersof"));

// !op
			newCommand = new Command("op");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "_op_ yourself to be able to use `mute`, `kick` or `ban` commands. (Only if configured at <http://botwinder.info/config>)";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				Role role = e.Server.DiscordServer.GetRole(e.Server.ServerConfig.RoleIDOperator);
				if( role == null )
				{
					await e.Message.Channel.SendMessage(string.Format("I'm really sorry, buuut `{0}op` feature is not configured! Poke your admin to set it up at <http://botwinder.info/config>", e.Server.ServerConfig.CommandCharacter));
					return;
				}
				if( !e.Message.Server.CurrentUser.ServerPermissions.ManageRoles )
				{
					await e.Message.Channel.SendMessage("I don't have `ManageRoles` permission.");
					return;
				}

				string response = "Go get em tiger!";
				try
				{
					if( e.Message.User.HasRole(role) )
					{
						await e.Message.User.RemoveRoles(role);
						response = "All done?";
					}
					else
					{
						await e.Message.User.AddRoles(role);
						response = "Go get em tiger!";
					}
				}catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null )
					{
						if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
							response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
						else
							response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
					}
				}
				await e.Message.Channel.SendMessage(response);
			};
			commands.Add(newCommand);

// !kick
			newCommand = new Command("kick");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameters `@user reason` where `@user` = user mention or id; `reason` = worded description why did you kick them out - they will receive this via PM.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				if( !e.Message.Server.CurrentUser.ServerPermissions.KickMembers )
				{
					await e.Message.Channel.SendMessage("I don't have necessary permissions.");
					return;
				}

				Role role = e.Server.DiscordServer.GetRole(e.Server.ServerConfig.RoleIDOperator);
				if( role != null && !e.Message.User.HasRole(role) && !client.IsGlobalAdmin(e.Message.User) )
				{
					await e.Message.Channel.SendMessage(string.Format("`{0}op`?", e.Server.ServerConfig.CommandCharacter));
					return;
				}

				Server<TUser> server = e.Server as Server<TUser>;
				User userToKick = null;

				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					await e.Message.Channel.SendMessage("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				if( e.Message.MentionedUsers == null || e.Message.MentionedUsers.Count() != 1 )
				{
					guid idToKick;
					if( !guid.TryParse(e.MessageArgs[0], out idToKick) || (userToKick = e.Message.Server.GetUser(idToKick)) == null )
					{
						await e.Message.Channel.SendMessage("Invalid arguments. (Are you sure that the target is on this server?)\n" + e.Command.Description);
						return;
					}
				}
				else
				{
					userToKick = e.Message.MentionedUsers.ElementAt(0);
				}

				if( client.GlobalConfig.UserIds.Contains(userToKick.Id) || client.GlobalConfig.OwnerIDs.Contains(userToKick.Id) || userToKick.Id == GlobalConfig.Rhea )
				{
					await e.Message.Channel.SendMessage("I don't think so...");
					return;
				}

				string reason = "";
				for(int i = 1; i < e.MessageArgs.Length; i++)
					reason += e.MessageArgs[i] + " ";

				if (idToBan == e.Message.Author.Id) {
					string reason = "Sorry, you cannot ban yourself. ¯\_(ツ)_/¯";
				}
				else
				{
					string response = "_\\*fires a railgun at <@"+ userToKick.Id.ToString() +">*_";

					if( !e.Message.Server.CurrentUser.ServerPermissions.KickMembers )
						response = "No can do. Please let the admin know that I don't have correct permissions.";
					else
					{
						try
						{
							client.UserBanned(userToKick, server.DiscordServer, DateTimeOffset.MinValue, reason, true, e.Message.User);
							await userToKick.SendMessage(string.Format("Hello!\nI regret to inform you, that you have been **kicked out of the {0} server** for the following reason:\n{1}\n\n_(You can rejoin the server in a few minutes.)_", server.Name, reason));
							await Task.Delay(500);
							await userToKick.Kick();
							server.UserDatabase.AddWarning(userToKick, "Kicked: "+reason);
						} catch(Exception exception)
						{
							Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
							if( ex != null )
							{
								if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
									response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
								else
									response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
							}
							else
							{
								client.LogException(exception, e);
								response = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
							}
						}
					}
				}

				await e.Message.Channel.SendMessage(response);
			};
			commands.Add(newCommand);

// !ban
			newCommand = new Command("ban");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameters `@user time reason` where `@user` = user mention or id; `time` = duration of the ban (e.g. `7d` or `12h` or `0` for permanent.); `reason` = worded description why did you ban them - they will receive this via PM (use `silentBan` to not send the PM)";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				if( !e.Message.Server.CurrentUser.ServerPermissions.BanMembers )
				{
					await e.Message.Channel.SendMessage("I don't have necessary permissions.");
					return;
				}

				Role role = e.Server.DiscordServer.GetRole(e.Server.ServerConfig.RoleIDOperator);
				if( role != null && !e.Message.User.HasRole(role) && !client.IsGlobalAdmin(e.Message.User) )
				{
					await e.Message.Channel.SendMessage(string.Format("`{0}op`?", e.Server.ServerConfig.CommandCharacter));
					return;
				}

				if( e.MessageArgs == null || e.MessageArgs.Length < 3 )
				{
					await e.Message.Channel.SendMessage("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				Server<TUser> server = e.Server as Server<TUser>;
				guid idToBan;
				int banDurationHours = 0;
				Match dayMatch = Regex.Match(e.MessageArgs[1], "\\d+d", RegexOptions.IgnoreCase);
				Match hourMatch = Regex.Match(e.MessageArgs[1], "\\d+h", RegexOptions.IgnoreCase);

				if( !hourMatch.Success && !dayMatch.Success && !int.TryParse(e.MessageArgs[1], out banDurationHours) )
				{
					await e.Message.Channel.SendMessage("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				if( hourMatch.Success )
					banDurationHours = int.Parse(hourMatch.Value.Trim('h'));
				if( dayMatch.Success )
					banDurationHours += 24 * int.Parse(dayMatch.Value.Trim('d'));

				if( e.Message.MentionedUsers == null || e.Message.MentionedUsers.Count() != 1 )
				{
					if( !guid.TryParse(e.MessageArgs[0], out idToBan) )
					{
						await e.Message.Channel.SendMessage("Invalid arguments.\n" + e.Command.Description);
						return;
					}
				}
				else
				{
					idToBan = e.Message.MentionedUsers.ElementAt(0).Id;
				}

				if( client.GlobalConfig.UserIds.Contains(idToBan) || client.GlobalConfig.OwnerIDs.Contains(idToBan) || idToBan == GlobalConfig.Rhea )
				{
					await e.Message.Channel.SendMessage("I don't think so...");
					return;
				}

				string reason = "";
				for(int i = 2; i < e.MessageArgs.Length; i++)
					reason += e.MessageArgs[i] + " ";

				if (idToBan == e.Message.Author.Id) {
					string reason = "Sorry, you cannot ban yourself. ¯\_(ツ)_/¯";
					await e.Message.Channel.SendMessage(response);
				}
				else
				{
					string response = "_\\*fires them railguns at <@"+ idToBan.ToString() +">*_  Ò_Ó";

					try
					{
						await client.Ban(idToBan, server, banDurationHours, reason, (e.Command.ID.ToLower() == "silentban"), (e.Command.ID.ToLower() == "purgeban"), bannedBy: e.Message.User);
					} catch(Exception exception)
					{
						Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
						if( ex != null )
						{
							if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
								response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
							else
								response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
						}
						else
						{
							client.LogException(exception, e);
							response = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
						}
					}

					await e.Message.Channel.SendMessage(response);
					if( !response.Contains("permissions") && !response.Contains("error") && !response.Contains("derp") )
					{
						try
						{
							await SendBanImage(e.Message.Channel);
						} catch(Exception exception)
						{

							client.LogException(exception, null, "OnUserBanned.SendFile failed");
						}
					}
				}
			};
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("silentBan");
			newCommand.Description = "Use with the same parameters like `ban`. The _reason_ message will not be sent to the user (hence silent.)";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("silentban"));

			newCommand = newCommand.CreateCopy("purgeBan");
			newCommand.Description = "Use with the same parameters like `ban`. The difference is that this command will also delete all the messages of the user in last 24 hours.";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("purgeban"));

// !quickBan
			newCommand = new Command("quickBan");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Quickly ban someone using pre-configured reason and duration, it also removes their messages. You can mention several people at once. (This command has to be first configured via `config` or <http://botwinder.info/config>.)";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				if( !e.Message.Server.CurrentUser.ServerPermissions.BanMembers )
				{
					await e.Message.Channel.SendMessage("I don't have necessary permissions.");
					return;
				}

				if( string.IsNullOrWhiteSpace(e.Server.ServerConfig.QuickbanReason) )
				{
					await e.Message.Channel.SendMessage("I'm really sorry, buuut Quickban feature is not configured! Poke your admin to set it up at <http://botwinder.info/config>");
					return;
				}
				if( string.IsNullOrWhiteSpace(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessage("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				Server<TUser> server = e.Server as Server<TUser>;
				List<guid> idsToBan;
				if( e.Message.MentionedUsers == null || e.Message.MentionedUsers.Count() == 0 )
				{
					idsToBan = new List<guid>();
					for(int i = 0; i < e.MessageArgs.Length; i++)
					{
						guid idToBan = 0;
						if( !guid.TryParse(e.MessageArgs[i], out idToBan) )
						{
							await e.Message.Channel.SendMessage("Invalid arguments, the parameter at the `"+ i +"` index is not a valid UserID\n" + e.Command.Description);
							return;
						}

						idsToBan.Add(idToBan);
					}
				}
				else
				{
					idsToBan = new List<guid>(e.Message.MentionedUsers.Select(u => u.Id));
				}

				if (idSToBanContains(e.Message.Author.Id) == True) {
					string reason = "Sorry, you cannot ban yourself. ¯\_(ツ)_/¯";
					await e.Message.Channel.SendMessage(response);
				}
				else
				{
					string response = "_\\*fires them railguns at "+ Utils.GetUserMentions(idsToBan) +"*_  Ò_Ó";

					try
					{
						foreach(guid id in idsToBan)
						{
							if( !client.GlobalConfig.OwnerIDs.Contains(id) && id != GlobalConfig.Rhea )
								await client.Ban(id, server, server.ServerConfig.QuickbanDuration, server.ServerConfig.QuickbanReason, false, true, bannedBy: e.Message.User);
						}
					} catch(Exception exception)
					{
						Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
						if( ex != null )
						{
							if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
								response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
							else
								response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
						}
						else
						{
							client.LogException(exception, e);
							response = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
						}
					}

					await e.Message.Channel.SendMessage(response);
					if( !response.Contains("permissions") && !response.Contains("error") && !response.Contains("derp") )
					{
						try
						{
							await SendBanImage(e.Message.Channel);
						} catch(Exception exception)
						{

							client.LogException(exception, null, "OnUserBanned.SendFile failed");
						}
					}
				}
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("quickban"));

// !unban
			newCommand = new Command("unban");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameter `@user` where `@user` = user mention or id;";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				guid id;
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessage("Invalid arguments.\n" + e.Command.Description);
					return;
				}

				if( e.Message.MentionedUsers == null || e.Message.MentionedUsers.Count() != 1 )
				{
					if( !guid.TryParse(e.TrimmedMessage, out id) )
					{
						await e.Message.Channel.SendMessage("Invalid arguments.\n" + e.Command.Description);
						return;
					}
				}
				else
				{
					id = e.Message.MentionedUsers.ElementAt(0).Id;
				}

				string response = "ó_ò";

				try
				{
					await client.UnBan(id, e.Server as Server<TUser>);
				} catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null )
					{
						if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
							response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
						else
							response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
					}
					else
					{
						client.LogException(exception, e);
						response = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
					}
				}

				await e.Message.Channel.SendMessage(response);
			};
			commands.Add(newCommand);

// !muteChannel
			newCommand = new Command("muteChannel");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Mute the current channel temporarily - duration configured at the website.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				if( !e.Message.Server.CurrentUser.ServerPermissions.ManageChannels && !e.Message.Server.CurrentUser.ServerPermissions.Administrator )
				{
					await e.Message.Channel.SendMessage("I don't have `ManageChannels` permission >_<");
					return;
				}

				Role opRole = e.Server.DiscordServer.GetRole(e.Server.ServerConfig.RoleIDOperator);
				if( opRole != null && !e.Message.User.HasRole(opRole) && !client.IsGlobalAdmin(e.Message.User) )
				{
					await e.Message.Channel.SendMessage(string.Format("`{0}op`?", e.Server.ServerConfig.CommandCharacter));
					return;
				}

				Message msg = await e.Message.Channel.SendMessage("This channel is temporarily muted, in order to pay respects to the ded ones.");

				if( e.Server.ServerConfig.MutedChannels == null )
					e.Server.ServerConfig.MutedChannels = new guid[1];
				else
					Array.Resize<guid>(ref e.Server.ServerConfig.MutedChannels, e.Server.ServerConfig.MutedChannels.Length +1);

				int channelIndex = e.Server.ServerConfig.MutedChannels.Length -1;
				e.Server.ServerConfig.MutedChannels[channelIndex] = e.Message.Channel.Id;
				e.Server.ServerConfig.MuteDuration = (e.Server.ServerConfig.MuteDuration < 5 ? 5 : e.Server.ServerConfig.MuteDuration > 60 ? 60 : e.Server.ServerConfig.MuteDuration);
				e.Server.ServerConfig.SaveAsync();

				Role role = e.Message.Server.GetRole(e.Message.Server.Id);
				await Task.Delay(TimeSpan.FromSeconds(1f));
				await e.Message.Channel.AddPermissionsRule(role, new ChannelPermissionOverrides(basePerms: e.Message.Channel.GetPermissionsRule(role), sendMessages: PermValue.Deny));
				await e.Message.Delete();

				await Task.Delay(TimeSpan.FromMinutes(e.Server.ServerConfig.MuteDuration));
				await e.Message.Channel.AddPermissionsRule(role, new ChannelPermissionOverrides(basePerms: e.Message.Channel.GetPermissionsRule(role), sendMessages: PermValue.Inherit));
				await msg.Edit("~~This channel is temporarily muted, in order to pay respects to the ded ones.~~\nThe silence has been lifted. You can now press **`F`** to pay your respects.");
				if( e.Server.ServerConfig.MutedChannels.Contains(e.Message.Channel.Id) )
					e.Server.ServerConfig.MutedChannels[channelIndex] = 0;
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("mutechannel"));

// !unmuteChannel
			newCommand = new Command("unmuteChannel");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Mute the current channel temporarily - duration configured at the website.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				if( !e.Message.Server.CurrentUser.ServerPermissions.ManageChannels && !e.Message.Server.CurrentUser.ServerPermissions.Administrator )
				{
					await e.Message.Channel.SendMessage("I don't have `ManageChannels` permission >_<");
					return;
				}

				if( e.Server.ServerConfig.MutedChannels == null || e.Server.ServerConfig.MutedChannels.Length == 0 || !e.Server.ServerConfig.MutedChannels.Contains(e.Message.Channel.Id) )
				{
					await e.Message.Channel.SendMessage("Nothing muted here, at least not by me anyway... Check your channel permissions, if your minions can't talk. x_x");
					return;
				}

				for(int i = 0; i < e.Server.ServerConfig.MutedChannels.Length; i++)
					if( e.Server.ServerConfig.MutedChannels[i] == e.Message.Channel.Id )
						e.Server.ServerConfig.MutedChannels[i] = 0;

				Role role = e.Message.Server.GetRole(e.Message.Server.Id);
				await e.Message.Channel.AddPermissionsRule(role, new ChannelPermissionOverrides(basePerms: e.Message.Channel.GetPermissionsRule(role), sendMessages: PermValue.Inherit));

				await e.Message.Channel.SendMessage("_\\*Presses `F` to pay respects, as the silence ends.*_");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("unmutechannel"));

// !mute
			newCommand = new Command("mute");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Temporarily mute mentioned members from both the chat and voice. This command has to be configured at <http://botwinder.info/config>!";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				Role role = e.Message.Server.GetRole(e.Server.ServerConfig.MuteRole);
				if( !e.Message.Server.CurrentUser.ServerPermissions.ManageRoles )
				{
					await e.Message.Channel.SendMessage("I don't have `ManageRoles` permission >_<");
					return;
				}
				if( role == null )
				{
					await e.Message.Channel.SendMessage("This command has to be configured at <http://botwinder.info/config>!");
					return;
				}
				if( !e.Message.MentionedUsers.Any() )
				{
					await e.Message.Channel.SendMessage("And who would you like me to ~~kill~~ _silence_?");
					return;
				}

				Role opRole = e.Server.DiscordServer.GetRole(e.Server.ServerConfig.RoleIDOperator);
				if( opRole != null && !e.Message.User.HasRole(opRole) && !client.IsGlobalAdmin(e.Message.User) )
				{
					await e.Message.Channel.SendMessage(string.Format("`{0}op`?", e.Server.ServerConfig.CommandCharacter));
					return;
				}

				string response = "";
				try
				{
					List<User> mutedUsers = new List<User>();
					lock(client.ServersLock)
					{
						foreach(User user in e.Message.MentionedUsers)
						{
							if( e.Server.IsAdmin(user) || e.Server.IsModerator(user) || (e.Server.ServerConfig.MutedUsers != null && e.Server.ServerConfig.MutedUsers.Contains(user.Id)) )
								continue;

							if( e.Server.ServerConfig.MutedUsers == null )
								e.Server.ServerConfig.MutedUsers = new guid[1];
							else
								Array.Resize<guid>(ref e.Server.ServerConfig.MutedUsers, e.Server.ServerConfig.MutedUsers.Length + 1);

							e.Server.ServerConfig.MutedUsers[e.Server.ServerConfig.MutedUsers.Length - 1] = user.Id;
							e.Server.ServerConfig.MuteDuration = (e.Server.ServerConfig.MuteDuration < 5 ? 5 : e.Server.ServerConfig.MuteDuration > 60 ? 60 : e.Server.ServerConfig.MuteDuration);
							e.Server.ServerConfig.SaveAsync();

							mutedUsers.Add(user);
						}
					}

					foreach(User user in mutedUsers)
					{
						await client.MuteUser(e.Server as Server<TUser>, user, e.Message.User);
					}

					await Task.Delay(100);
					string userNames = Utils.GetUserMentions(mutedUsers.Select(u => u.Id).ToList());
					if( !string.IsNullOrWhiteSpace(userNames) )
					{
						response = "*Silence!!  ò_ó\n...\nI keel u, " + userNames + "!!*  Ò_Ó";

						Channel ignoreChannel = e.Server.DiscordServer.GetChannel(e.Server.ServerConfig.MuteIgnoreChannel);
						if( ignoreChannel != null )
							await ignoreChannel.SendMessage("Welcome to the afterlife, " + userNames + ".");
					}
					else
					{
						response = "I can not mute Admins, Moderators, or people who are already _technically_ muted.\n_(If you failed to mute them the first time due to permissions, you still have to unmute them before trying to mute them again.)_";
					}

					await e.Message.Channel.SendMessage(response);
					response = null;

					await Task.Delay(TimeSpan.FromMinutes(e.Server.ServerConfig.MuteDuration));
					foreach(User user in mutedUsers)
					{
						await client.UnmuteUser(e.Server as Server<TUser>, user, e.Server.DiscordServer.CurrentUser);
					}
				}catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null )
					{
						if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
							response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
						else
							response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
					}
				}
				if( !string.IsNullOrWhiteSpace(response) )
					await e.Message.Channel.SendMessage(response);
			};
			commands.Add(newCommand);

// !unmute
			newCommand = new Command("unmute");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Unmute previously muted members. This command has to be configured at <http://botwinder.info/config>.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				Role role = e.Message.Server.GetRole(e.Server.ServerConfig.MuteRole);
				if( !e.Message.Server.CurrentUser.ServerPermissions.ManageRoles )
				{
					await e.Message.Channel.SendMessage("I don't have `ManageRoles` permission >_<");
					return;
				}
				if( role == null )
				{
					await e.Message.Channel.SendMessage("This command has to be configured at <http://botwinder.info/config>.");
					return;
				}

				foreach(User user in e.Message.MentionedUsers)
				{
					await client.UnmuteUser(e.Server as Server<TUser>, user, e.Message.User);
				}
				string userNames = Utils.GetUserMentions(e.Message.MentionedUsers.Select(u => u.Id).ToList());
				await e.Message.Channel.SendMessage(userNames+" speak!");
			};
			commands.Add(newCommand);

// !whois
			newCommand = new Command("whois");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Search for a User on this server.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessage("Who are you looking for?");
					return;
				}

				List<User> foundUsers = new List<User>(e.Message.MentionedUsers);

				if( e.Message.MentionedUsers == null || e.Message.MentionedUsers.Count() == 0 )
				{
					guid id;
					User foundUser = null;
					if( guid.TryParse(e.TrimmedMessage, out id) && (foundUser = e.Message.Server.GetUser(id)) != null )
					{
						TUser userData = (e.Server as Server<TUser>).UserDatabase.GetOrAddUser(foundUser);
						string whoisString = userData.GetWhoisString(foundUser);
						while( whoisString.Length > GlobalConfig.MessageCharacterLimit )
						{
							int splitIndex = GlobalConfig.MessageCharacterLimit;
							while(whoisString[--splitIndex] != ' ');
							await e.Message.Channel.SendMessage(whoisString.Substring(0, splitIndex));
							whoisString = whoisString.Substring(splitIndex);
						}
						await e.Message.Channel.SendMessage(whoisString);
						return;
					}

					foreach(User user in e.Message.Server.Users)
					{
						if( user != null && !string.IsNullOrEmpty(user.Name) && user.Name.ToLower().Contains(e.TrimmedMessage.ToLower()) )
							foundUsers.Add(user);
					}

					if( foundUsers == null || foundUsers.Count == 0 )
					{
						await e.Message.Channel.SendMessage("I couldn't find them on this server, try using `!find` to search the whole database :)");
						return;
					}
					if( foundUsers.Count > GlobalConfig.WhoisCommandLimit )
					{
						await e.Message.Channel.SendMessage("Be more specific please, I found way too many!");
						return;
					}
				}

				foreach(User user in foundUsers)
				{
					TUser userData = (e.Server as Server<TUser>).UserDatabase.GetOrAddUser(user);
					string whoisString = userData.GetWhoisString(user.Server.Id == e.Server.ID ? user : null);
					while( whoisString.Length > GlobalConfig.MessageCharacterLimit )
					{
						await e.Message.Channel.SendMessage(whoisString.Substring(0, GlobalConfig.MessageCharacterLimit));
						whoisString = whoisString.Substring(GlobalConfig.MessageCharacterLimit);
					}
					await e.Message.Channel.SendMessage(whoisString);
				}
			};
			commands.Add(newCommand);

// !find
			newCommand = new Command("find");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Find a User in the database.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessage("Who are you looking for?");
					return;
				}

				guid id;
				TUser userData = null;
				UserDatabase<TUser> database = (e.Server as Server<TUser>).UserDatabase;
				if( guid.TryParse(e.TrimmedMessage, out id) && database.TryGetValue(id, out userData) )
				{
					User user = e.Message.Server.GetUser(userData.ID);
					string whoisString = userData.GetWhoisString(user);
					while( whoisString.Length > GlobalConfig.MessageCharacterLimit )
					{
						int splitIndex = GlobalConfig.MessageCharacterLimit;
						while(whoisString[--splitIndex] != ' ');
						await e.Message.Channel.SendMessage(whoisString.Substring(0, splitIndex));
						whoisString = whoisString.Substring(splitIndex);
					}
					await e.Message.Channel.SendMessage(whoisString);
				}
				else
				{
					List<TUser> foundUsers = database.FindAll(e.TrimmedMessage);
					if( foundUsers == null || foundUsers.Count == 0 )
					{
						await e.Message.Channel.SendMessage("I'm sorry but I couldn't find them :(");
						return;
					}
					if( foundUsers.Count > GlobalConfig.WhoisCommandLimit )
					{
						await e.Message.Channel.SendMessage("Be more specific please, I found way too many!");
						return;
					}

					foreach(TUser foundUserData in foundUsers)
					{
						User user = e.Message.Server.GetUser(foundUserData.ID);
						string whoisString = foundUserData.GetWhoisString(user);
						while( whoisString.Length > GlobalConfig.MessageCharacterLimit )
						{
							await e.Message.Channel.SendMessage(whoisString.Substring(0, GlobalConfig.MessageCharacterLimit));
							whoisString = whoisString.Substring(GlobalConfig.MessageCharacterLimit);
						}
						await e.Message.Channel.SendMessage(whoisString);
					}
				}

			};
			commands.Add(newCommand);

// !addWarning
			newCommand = new Command("addWarning");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameters `@user warning` where `@user` = user mention or id, you can add the same warning to multiple people, just mention them all; `warning` = worded description, a warning message to store in the database.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessage("You're missing something x_X");
					return;
				}

				List<TUser> mentionedUsers = Utils.GetMentionedUsersData<TUser>(e);

				if( mentionedUsers.Count == 0 )
				{
					await e.Message.Channel.SendMessage("I couldn't find them :(");
					return;
				}

				string warning = "";
				for(int i = mentionedUsers.Count; i < e.MessageArgs.Length; i++)
				{
					warning += e.MessageArgs[i] + " ";
				}

				bool sendMessage = e.Command.ID.ToLower() == "issuewarning";
				foreach(TUser userData in mentionedUsers)
				{
					userData.AddWarning(warning);
					if( sendMessage )
					{
						User user = e.Server.DiscordServer.GetUser(userData.ID);
						if( user != null )
							user.SendMessage(string.Format("Hello!\nYou have been issued a formal **warning** by the Moderators of the **{0} server** for the following reason:\n{1}",
								e.Server.DiscordServer.Name, warning));
					}
				}
				(e.Server as Server<TUser>).UserDatabase.SaveAsync();
				await e.Message.Channel.SendMessage("It has been done!");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("addwarning"));

			newCommand = newCommand.CreateCopy("issueWarning");
			newCommand.Description = "Use with parameters `@user warning` where `@user` = user mention or id, you can add the same warning to multiple people, just mention them all; `warning` = worded description, a warning message to store in the database. This will also be PMed to the user.";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("issuewarning"));

// !removeWarning
			newCommand = new Command("removeWarning");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameter `@user` = user mention or id, you can remove the last warning from multiple people, just mention them all.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessage("You're missing something x_X");
					return;
				}

				List<TUser> mentionedUsers = Utils.GetMentionedUsersData<TUser>(e);

				if( mentionedUsers.Count == 0 )
				{
					await e.Message.Channel.SendMessage("I couldn't find them :(");
					return;
				}

				foreach(TUser user in mentionedUsers)
				{
					if( e.Command.ID.ToLower() == "removeallwarnings" )
						user.RemoveAllWarnings();
					else
						user.RemoveLastWarning();
				}

				(e.Server as Server<TUser>).UserDatabase.SaveAsync();
				await e.Message.Channel.SendMessage("It has been done!");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removewarning"));
			newCommand = newCommand.CreateCopy("removeAllWarnings");
			newCommand.Description = "Use with parameter `@user` = user mention or id, you can remove all the warnings from multiple people, just mention them all.";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removeallwarnings"));

// !naughtyList
			newCommand = new Command("naughtyList");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Return a list of everyone who has a warning.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				int count = (e.Server as Server<TUser>).UserDatabase._UserData.Count(u => u.WarningCount > 0);

				string response = "I found "+ count.ToString() +" naughty people:\n";
				await (e.Server as Server<TUser>).UserDatabase.ForEach(async userData =>{
					if( userData.WarningCount > 0 )
					{
						string newString = "\n" + userData.GetWhoisString();
						if( newString.Length + response.Length > GlobalConfig.MessageCharacterLimit )
						{
							await e.Message.Channel.SendMessage(response);
							response = "";

							while( newString.Length > GlobalConfig.MessageCharacterLimit )
							{
								int splitIndex = GlobalConfig.MessageCharacterLimit;
								while(newString[--splitIndex] != ' ');
								await e.Message.Channel.SendMessage(newString.Substring(0, splitIndex));
								newString = newString.Substring(splitIndex);
							}
						}
						response += newString;
					}
				});

				await e.Message.Channel.SendMessage(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("naughtylist"));

// !clear
			newCommand = new Command("clear");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameter `n` - number of messages to delete. You can also mention a user to delete only their messages, e.g. `!clear 3 @user` - )";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				Operation op = null;
				try
				{
					if( !e.Message.Server.CurrentUser.ServerPermissions.ManageMessages )
					{
						await e.Message.Channel.SendMessage("I don't have `ManageMessages` permission >_<");
						return;
					}

					int n = 0;
					Message msg = null;
					List<User> users = Utils.GetMentionedUsers(e);
					IEnumerable<guid> userIDs = users.Select(u => u.Id);
					string userNames = Utils.GetUserNames(users);

					if( users.Count == 0 && e.MessageArgs != null && (e.MessageArgs.Length > 1 || (e.MessageArgs.Length == 1 && e.Command.ID == "nuke")) )
					{
						guid id = 0;
						if( !guid.TryParse((e.Command.ID == "nuke" ? e.MessageArgs[0] : e.MessageArgs[1]).TrimStart('<', '@', '!').TrimEnd('>'), out id) )
						{
							await e.Message.Channel.SendMessage("I can see that you're trying to use more parameters, but I did not find any IDs or mentions.");
							return;
						}

						userIDs = new List<guid> { id };
					}

					bool clearLinks = e.Command.ID.ToLower() == "clearlinks";
					if( clearLinks && users.Any() )
					{
						await e.Message.Channel.SendMessage("`"+ e.Server.ServerConfig.CommandCharacter +"clearLinks` does not take `@user` mentions as parameter.");
						return;
					}

					if( e.Command.ID == "nuke" )
					{
						n = int.MaxValue - 1;
						if( users.Count > 0 )
							msg = await e.Message.Channel.SendMessage("Deleting all the messages by " + userNames + ".");
						else
							msg = await e.Message.Channel.SendMessage("Nuking the channel, I'll tell you when I'm done (large channels may take up to half an hour...)");
					}
					else if( e.MessageArgs == null || e.MessageArgs.Length < 1 || !int.TryParse(e.MessageArgs[0], out n) )
					{
						await e.Message.Channel.SendMessage("Please tell me how many messages should I delete!");
						return;
					}
					else if( users.Count > 0 )
					{
						msg = await e.Message.Channel.SendMessage("Deleting " + n.ToString() + " messages by " + userNames + ".");
					}
					else
						msg = await e.Message.Channel.SendMessage("Deleting " + (clearLinks ? "attachments and embeds in " : "") + n.ToString() + " messages.");

					guid lastRemoved = e.Message.Id;
					int userCount = userIDs.Count();

					if( n > GlobalConfig.LargeOperationThreshold )
					{
						op = Operation.Create<TUser>(client, e);
						if( await op.Await(client, async () => await msg.Edit(msg.RawText + string.Format("\n"+ GlobalConfig.OperationQueuedText, client.CurrentOperations.Count, e.Command.ID))) )
						{
							await msg.Edit("This operation was canceled. (Either manually or it is a duplicate of already queued up command. Be patient please.)");
							return;
						}
						op.CurrentState = Operation.State.Running;
					}

					int cyclesToYield = 5;
					int exceptions = 0;
					bool reachedTwoWeeks = false;
					while( n > 0 )
					{
						if( op != null && await op.AwaitConnection(client) )
						{
							await msg.Edit("This operation was canceled.");
							return;
						}

						Message[] messages = null;

						try
						{
							messages = await e.Message.Channel.DownloadMessages((userCount > 0 ? Math.Min(100, n) : 100), lastRemoved, useCache: false);
						} catch( Exception exception )
						{
							client.LogException(exception, e);
							lastRemoved = 0;
							break;
						}

						List<guid> ids = null;
						Func<Message, bool> isWithinTwoWeeks = (Message m) =>{
							if( DateTime.UtcNow - (new DateTime((long)(((m.Id / 4194304) + 1420070400000) * 10000 + 621355968000000000))) < TimeSpan.FromDays(13.9f) )
								return true;
							reachedTwoWeeks = true;
							return false;
						};
						if( messages == null || messages.Length == 0 ||
						    ( clearLinks && userCount == 0 && (ids = messages.TakeWhile(m => isWithinTwoWeeks(m)).Where(m => (m.Attachments != null && m.Attachments.Length > 0) || (m.Embeds != null && m.Embeds.Length > 0)).Select(m => m.Id).ToList()).Count == 0) ||
						    (!clearLinks && userCount == 0 && (ids = messages.TakeWhile(m => isWithinTwoWeeks(m)).Select(m => m.Id).ToList()).Count == 0) ||
						    (userCount > 0 && (ids = messages.TakeWhile(m => isWithinTwoWeeks(m)).Where(m => (m == null || m.User == null ? false : userIDs.Contains(m.User.Id))).Select(m => m.Id).ToList()).Count == 0) )
						{
							lastRemoved = e.Message.Id;
							break;
						}

						//messages.TakeWhile(m => DateTime.UtcNow - (new DateTime((long)(((messages.First().Id / 4194304) + 1420070400000) * 10000 + 621355968000000000))) < TimeSpan.FromDays(14))

						if( !client.ClearedMessageIDs.ContainsKey(e.Server.ID) )
							client.ClearedMessageIDs.Add(e.Server.ID, new List<guid>());

						lastRemoved = messages.Last().Id;
						if( ids.Count > n )
							ids = ids.Take(n).ToList();
						client.ClearedMessageIDs[e.Server.ID].AddRange(ids);

						try
						{
							await e.Message.Channel.DeleteMessages(ids.ToArray());
						} catch( Exception exception )
						{
							if( ++exceptions > 10 || exception.Message.Contains("50034") )
								break;

							//Continue if it this fails.
						}

						n -= ids.Count;
						if( messages.Length < 100 ) //this was the last pull
							n = 0;

						if( --cyclesToYield <= 0 )
						{
							cyclesToYield = 5;
							await Task.Yield(); //Take a break, do other things!
						}
					}

					if( lastRemoved == 0 )
						await msg.Edit("There was an error while downloading messages, you can try again but if it doesn't work, then it's a bug - please tell Rhea :<");
					else
					{
						if( !e.Message.Deleted )
							await e.Message.Delete();

						await msg.Edit("~~" + msg.RawText + "~~\n\nDone! _(This message will self-destruct in 10 seconds.)_");
						if( reachedTwoWeeks )
							await e.Message.Channel.SendMessage("I couldn't delete all of it though, sowwy :<\n_(Discord Developers have decided to shut-down our ability to delete messages older than two weeks. <https://github.com/hammerandchisel/discord-api-docs/issues/208>)_");
						await Task.Delay(TimeSpan.FromSeconds(10f));
						await msg.Edit("BOOM!!");
						await Task.Delay(TimeSpan.FromSeconds(2f));
						await msg.Delete();
					}
				} catch( Exception exception ) when( exception.GetType() != typeof(Discord.Net.HttpException) )
				{
					client.LogException(exception, e);
				}

				if( op != null )
					op.Finalise(client);
				op = null;
			};
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("nuke");
			newCommand.Description = "Nuke the whole channel. You can also mention a user to delete all of their messages.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("clearLinks");
			newCommand.Description = "Delete only messages that contain links. Use with the same parameters as regular _clear_ - number of messages";
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("clearlinks"));

// !memberRoles
			newCommand = new Command("memberRoles");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "See what Member Roles can you assign.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				string response = string.Format("You can use `{0}promote` and `{0}demote` commands with these Roles: ", (e.Server as Server<TUser>).ServerConfig.CommandCharacter);

				guid[] memberRoles = (e.Server as Server<TUser>).ServerConfig.RoleIDsMember;
				if( memberRoles == null || memberRoles.Length == 0 )
				{
					response = "I'm sorry, but there are no Member roles on this server.";
				}
				else
				{
					for(int i = 0; i < memberRoles.Length; i++)
					{
						string newString;
						Role role = e.Message.Server.GetRole(memberRoles[i]);
						if( role != null )
						{
							newString = (i == 0 ? "`" : i == memberRoles.Length -1 ? " and `" : ", `") + role.Name +"`";
							if( newString.Length + response.Length > GlobalConfig.MessageCharacterLimit )
							{
								await e.Message.Channel.SendMessage(response);
								response = "";
							}
							response += newString;
						}
					}
				}

				await e.Message.Channel.SendMessage(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("memberroles"));

// !promote
			newCommand = new Command("promote");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Assign a Member role to the user. Use with parameters `@user role` where `@user` = user mention or id; and `role` = case sensitive name of the role to assign.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				Role role = null;
				string message = "";
				List<User> mentionedUsers = Utils.GetMentionedUsers(e);
				guid[] memberRoles = (e.Server as Server<TUser>).ServerConfig.RoleIDsMember;
				guid[] secureMemberRoles = (e.Server as Server<TUser>).ServerConfig.RoleIDsSecureMember;

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					message = "You're missing something x_X";
				}
				else if( (memberRoles == null || memberRoles.Length == 0) && (secureMemberRoles == null || secureMemberRoles.Length == 0) )
				{
					message = "I'm sorry, but there are no Member roles on this server.";
				}
				else if( mentionedUsers.Count == 0 )
				{
					message = "I couldn't find them :(";
				}
				else
				{
					string roleName = "";
					for(int i = mentionedUsers.Count; i < e.MessageArgs.Length; i++)
					{
						roleName += e.MessageArgs[i] + " ";
					}
					roleName = roleName.Trim();

					try
					{
						foreach(User user in mentionedUsers)
						{
							if( !string.IsNullOrWhiteSpace( message = await (e.Server as Server<TUser>).AssignRole(user, roleName,
								r => memberRoles.Contains((role = r).Id) || (secureMemberRoles != null && (e.Server.IsAdmin(e.Message.User) || e.Server.IsModerator(e.Message.User)) && secureMemberRoles.Contains((role = r).Id))) ) )
								message += " ("+user.Name+")";
							else
							{
								message = "Consider it Done!";

								Channel logChannel = null;
								if( e.Server.ServerConfig.ModChannelLogMembers && (logChannel = e.Message.Server.GetChannel(e.Server.ServerConfig.ModChannel)) != null )
								{
									string logMessage = string.Format("`{0}`: __{1}__ promoted __{2}__ to _{3}_.", Utils.GetTimestamp(), e.Message.User.Name, user.Name, (role == null ? roleName : role.Name));
									await logChannel.SendMessage(logMessage);
								}
							}
						}
					} catch(Exception exception)
					{
						Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
						if( ex != null )
						{
							if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
								message = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
							else
								message = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
						}
						else
						{
							client.LogException(exception, e);
							message = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
						}
					}
				}

				Message msg = await e.Message.Channel.SendMessage(message);
				if( e.Server.ServerConfig.RemovePromote )
				{
					await Task.Delay(TimeSpan.FromSeconds(10f));
					await e.Message.Delete();
					await Task.Delay(TimeSpan.FromSeconds(1f));
					await msg.Delete();
				}
			};
			commands.Add(newCommand);

// !demote
			newCommand = new Command("demote");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Remove a Member role from the user. Use with parameters `@user role` where `@user` = user mention or id; and `role` = case sensitive name of the role to remove.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				Role role = null;
				string message = "";
				guid[] memberRoles = (e.Server as Server<TUser>).ServerConfig.RoleIDsMember;
				guid[] secureMemberRoles = (e.Server as Server<TUser>).ServerConfig.RoleIDsSecureMember;
				List<User> mentionedUsers = Utils.GetMentionedUsers(e);
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					message = "You're missing something x_X";
				}
				else if( (memberRoles == null || memberRoles.Length == 0) && secureMemberRoles == null )
				{
					message = "I'm sorry, but there are no Member roles on this server.";
					return;
				}
				else if( mentionedUsers.Count == 0 )
				{
					message = "I couldn't find them :(";
					return;
				}
				else
				{
					string roleName = "";
					for(int i = mentionedUsers.Count; i < e.MessageArgs.Length; i++)
					{
						roleName += e.MessageArgs[i] + " ";
					}
					roleName = roleName.Trim();

					try
					{
						foreach(User user in mentionedUsers)
						{
							if( !string.IsNullOrWhiteSpace( message = await (e.Server as Server<TUser>).RemoveRole(user, roleName,
								r => memberRoles.Contains((role = r).Id) || (secureMemberRoles != null && (e.Server.IsAdmin(e.Message.User) || e.Server.IsModerator(e.Message.User)) && secureMemberRoles.Contains((role = r).Id))) ) )
								message += " ("+user.Name+")";
							else
							{
								message = "Consider it Done!";

								Channel logChannel = null;
								if( e.Server.ServerConfig.ModChannelLogMembers && (logChannel = e.Message.Server.GetChannel(e.Server.ServerConfig.ModChannel)) != null )
								{
									string logMessage = string.Format("`{0}`: __{1}__ demoted {2} from _{3}_.", Utils.GetTimestamp(), e.Message.User.Name, user.Name, (role == null ? roleName : role.Name));
									await logChannel.SendMessage(logMessage);
								}
							}
						}
					} catch(Exception exception)
					{
						Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
						if( ex != null )
						{
							if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
								message = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
							else
								message = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
						}
						else
						{
							client.LogException(exception, e);
							message = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
						}
					}
				}

				Message msg = await e.Message.Channel.SendMessage(message);
				if( e.Server.ServerConfig.RemovePromote )
				{
					await Task.Delay(TimeSpan.FromSeconds(10f));
					await e.Message.Delete();
					await Task.Delay(TimeSpan.FromSeconds(1f));
					await msg.Delete();
				}
			};
			commands.Add(newCommand);

// !ping
			newCommand = new Command("ping");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.SendTyping = false;
			newCommand.Description = "What's the response time of Botwinder?";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) => {
				await e.Message.Channel.SendMessage("I'm pinging myself!");
				await Task.Delay(1500);
				await e.Message.Channel.SendMessage("I'm looking for the ID of that first message cause I forgot what was it x_X");
				await Task.Delay(3000);
				await e.Message.Channel.SendMessage("Ah, got it! Now i'm gonna calculate its timetsamp!");
				await Task.Delay(2500);
				await e.Message.Channel.SendMessage("_This divided by this and then... hmm..._");
				await Task.Delay(2500);
				await e.Message.Channel.SendMessage("I'm gonna count the time now... One, two, three, ...");
				await Task.Delay(1500);
#pragma warning disable 4014
				e.Message.Channel.SendMessage("Juuust...");
				e.Message.Channel.SendMessage("...kidding! :D");
#pragma warning restore 4014
			};
			commands.Add(newCommand);

			return commands;
		}
	}
}
