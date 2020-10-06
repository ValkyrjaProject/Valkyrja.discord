using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Valkyrja.modules
{
	public class Moderation: IModule
	{
		private const int BanReasonLimit = 512;

		private const string ErrorUnknownString = "Unknown error, please poke <@{0}> to take a look x_x";
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		private const string BanPmString = "Hello!\nI regret to inform you, that you have been **banned {0} on the {1} server** for the following reason:\n{2}";
		private const string BanNotFoundString = "I couldn't find them :(";
		private const string NotFoundString = "User not found.";
		private const string NotFoundChannelString = "Channel not found.";
		private const string UnbanConfirmString = "I've unbanned {0}... ó_ò";
		private const string KickArgsString = "I'm supposed to shoot... who?\n";
		private const string KickNotFoundString = "I couldn't find them :(";
		private const string KickPmString = "Hello!\nYou have been kicked out of the **{0} server** by its Moderators for the following reason:\n{1}";
		private const string WarningNotFoundString = "I couldn't find them :(";
		private const string WarningPmString = "Hello!\nYou have been issued a formal **warning** by the Moderators of the **{0} server** for the following reason:\n{1}";
		private const string MuteIgnoreChannelString = "{0}, you've been muted.";
		private const string MuteChannelConfirmString = "Silence!!  ò_ó";
		private const string UnmuteConfirmString = "Speak {0}!";
		private const string UnmuteChannelConfirmString = "You may speak now.";
		private const string MuteNotFoundString = "And who would you like me to ~~kill~~ _silence_?";
		private const string InvalidArgumentsString = "Invalid arguments.\n";
		private const string BanReasonTooLongString = "Mute, Kick & Ban reason has 512 characters limit.\n";
		private const string RoleNotFoundString = "The Muted role is not configured - head to <https://valkyrja.app/config>";

		private ValkyrjaClient Client;

		private guid JustBannedId = 0;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.AddBan += AddBan;
			this.Client.Events.BanUser += Ban;
			this.Client.Events.BanUsers += Ban;
			this.Client.Events.UnBanUsers += UnBan;
			this.Client.Events.MuteUser += Mute;
			this.Client.Events.MuteUsers += Mute;
			this.Client.Events.UnMuteUsers += UnMute;

			this.Client.Events.UserJoined += OnUserJoined;


// !clear
			Command newCommand = new Command("clear");
			newCommand.Type = CommandType.Operation;
			newCommand.Description = "Deletes specified amount of messages (within two weeks.) If you mention someone as well, it will remove only their messages.";
			newCommand.ManPage = new ManPage("<n> [@users]", "`<n>` - The amount of messages to be deleted.\n\n`[@users]` - Optional argument specifying that only messages from these @users will be deleted. User mention(s) or ID(s)");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageMessages )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				int n = 0;
				RestUserMessage msg = null;
				List<guid> userIDs = this.Client.GetMentionedUserIds(e, false);

				if( e.Command.Id != "nuke" && (e.MessageArgs == null || e.MessageArgs.Length < 1 || !int.TryParse(e.MessageArgs[0], out n)) )
				{
					await e.SendReplySafe("Please tell me how many messages should I delete!");
					return;
				}

				Regex regex = null;
				bool clearRegex = e.Command.Id.ToLower() == "clearregex";
				if( clearRegex && e.MessageArgs.Length < 2 )
				{
					await e.SendReplySafe("Too few parameters.");
					return;
				}
				if( clearRegex )
					regex = new Regex($"({e.MessageArgs[1]})", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

				bool clearLinks = e.Command.Id.ToLower() == "clearlinks";

				int expectedArgsWithoutMentions = e.Command.Id == "nuke" ? 0 : clearRegex ? 2 : 1;
				if( e.MessageArgs != null && userIDs.Count < e.MessageArgs.Length - expectedArgsWithoutMentions )
				{
					await e.Message.Channel.SendMessageSafe("I can see that you're trying to use more parameters, but I did not find any IDs or mentions.");
					return;
				}

				if( e.Command.Id == "nuke" )
				{
					n = int.MaxValue - 1;
					if( userIDs.Count > 0 )
						msg = await e.Message.Channel.SendMessageAsync("Deleting all the messages by specified users.");
					else
						msg = await e.Message.Channel.SendMessageAsync("Nuking the channel, I'll tell you when I'm done (large channels may take up to half an hour...)");
				}
				else if( userIDs.Count > 0 )
				{
					msg = await e.Message.Channel.SendMessageAsync($"Deleting {n.ToString()} messages by specified users.");
				}
				else
					msg = await e.Message.Channel.SendMessageAsync($"Deleting {(clearLinks ? "attachments and embeds in the last " : clearRegex ? "regular expression matches in the last " : "")} {n.ToString()} message{(n == 1 ? "" : "s")}.");

				int userCount = userIDs.Count();
				guid lastRemoved = e.Message.Id;

				bool IsWithinTwoWeeks(IMessage m)
				{
					if( m == null )
						return false;
					if( DateTime.UtcNow - Utils.GetTimeFromId(m.Id) < TimeSpan.FromDays(13.9f) )
						return true;
					return false;
				}

				bool IsAuthorSpecific(IMessage m)
				{
					return userCount == 0 || (m?.Author != null && userIDs.Contains(m.Author.Id));
				}

				List<guid> idsToDelete = new List<guid>();
				IAsyncEnumerator<IReadOnlyCollection<IMessage>> enumerator = e.Message.Channel.GetMessagesAsync(lastRemoved, Direction.Before, int.MaxValue, CacheMode.AllowDownload).GetAsyncEnumerator();

				bool canceled = await e.Operation.While(() => n > 0, async () => {
					IMessage[] messages = null;

					try
					{
						await enumerator.MoveNextAsync();
						if( enumerator?.Current == null ) //Assume it's an empty channel?
							return true;
						if( !enumerator.Current.Any() )
						{
							await enumerator.MoveNextAsync(); //There seems to be an empty page if cache is empty.
							if( !(enumerator?.Current?.Any() ?? false) )
								return true;
						}

						messages = enumerator.Current.ToArray();
					}
					catch( HttpException exception )
					{
						if( await e.Server.HandleHttpException(exception, $"`{e.CommandId}` in <#{e.Channel.Id}>") )
						{
							n = 0;
							lastRemoved = 0; // Send error message to the channel.
							return true;
						}
					}
					catch( Exception exception )
					{
						await this.Client.LogException(exception, e);
						n = 0;
						lastRemoved = 0; // Send error message to the channel.
						return true;
					}

					List<guid> ids = null;
					if( messages == null || messages.Length == 0 ||
						(clearLinks && !(ids = messages.TakeWhile(IsWithinTwoWeeks).Where(IsAuthorSpecific).Where(m => (m.Attachments != null && m.Attachments.Any()) || (m.Embeds != null && m.Embeds.Any())).Select(m => m.Id).ToList()).Any()) ||
						(clearRegex  && !(ids = messages.TakeWhile(IsWithinTwoWeeks).Where(IsAuthorSpecific).Where(m => (!string.IsNullOrEmpty(m.Content) && regex.IsMatch(m.Content)) || (m.Embeds != null && m.Embeds.Any(em => regex.IsMatch(em.Title ?? "") || regex.IsMatch(em.Description ?? "") || regex.IsMatch(em.Author?.Name ?? "") || (em.Fields != null && em.Fields.Any(f => regex.IsMatch(f.Name ?? "") || regex.IsMatch(f.Value ?? "")))))).Select(m => m.Id).ToList()).Any()) ||
						(!clearLinks && !clearRegex && !(ids = messages.TakeWhile(IsWithinTwoWeeks).Where(IsAuthorSpecific).Select(m => m.Id).ToList()).Any()) )
					{
						n = 0;
						return true;
					}

					if( ids.Count > n )
						ids = ids.Take(n).ToList();

					idsToDelete.AddRange(ids);
					lastRemoved = ids.Last();

					n -= ids.Count;
					if( !IsWithinTwoWeeks(messages.Last()) )
						n = 0;

					return false;
				});

				await enumerator.DisposeAsync();

				if( canceled )
				{
					await e.SendReplySafe($"The command `{e.CommandId}` was cancelled.");
					return;
				}

				if( !this.Client.ClearedMessageIDs.ContainsKey(e.Server.Id) )
					this.Client.ClearedMessageIDs.Add(e.Server.Id, new List<guid>());

				this.Client.ClearedMessageIDs[e.Server.Id].AddRange(idsToDelete);
				this.Client.Monitoring.MsgsDeleted.Inc(idsToDelete.Count);

				int i = 0;
				guid[] chunk = new guid[100];
				guid[] idsToDeleteArray = idsToDelete.ToArray();
				canceled = await e.Operation.While(() => i < (idsToDeleteArray.Length + 99) / 100, async () => {
					try
					{
						if( idsToDeleteArray.Length <= 100 )
							await e.Channel.DeleteMessagesAsync(idsToDeleteArray);
						else
						{
							int chunkSize = idsToDeleteArray.Length - (100 * i);
							Array.Copy(idsToDeleteArray, i * 100, chunk, 0, Math.Min(chunkSize, 100));
							if( chunkSize < 100 )
								Array.Resize(ref chunk, chunkSize);
							await e.Channel.DeleteMessagesAsync(chunk);
						}

						i++;
					}
					catch( Discord.Net.HttpException exception )
					{
						if( await e.Server.HandleHttpException(exception, $"Failed command `{e.CommandId}` in <#{e.Channel.Id}>") )
							return true;
					}
					catch( Exception exception )
					{
						await this.Client.LogException(exception, e);
						return true;
					}

					return false;
				});

				if( clearRegex && !idsToDelete.Any() )
				{
					await msg.ModifyAsync(m => m.Content = $"~~{msg.Content}~~\n\nNothing matched that regex.");
				}

				if( canceled )
					return;

				try
				{
					if( lastRemoved == 0 )
						await msg.ModifyAsync(m => m.Content = "There was an error while downloading messages, you can try again but if it doesn't work, then it's a bug - please tell Rhea :<");
					else
					{
						if( !e.Message.Deleted )
							await e.Message.DeleteAsync();

						await msg.ModifyAsync(m => m.Content = $"~~{msg.Content}~~\n\nDone! _(This message will self-destruct in 10 seconds.)_");
						await Task.Delay(TimeSpan.FromSeconds(10f));
						await msg.ModifyAsync(m => m.Content = "BOOM!!");
						await Task.Delay(TimeSpan.FromSeconds(2f));
						await msg.DeleteAsync();
					}
				}
				catch( HttpException ex )
				{
					await e.Server.HandleHttpException(ex, $"Failed to send or delete a message in <#{e.Channel.Id}>");
				}
				catch( Exception ex )
				{
					await this.HandleException(ex, "clear cmd - failed to send or delete a message", e.Server.Id);
				}
			};
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("clearLinks");
			newCommand.Description = "Delete only messages that contain attachments (images, video, files, ...) or embeds including automated link embeds.";
			newCommand.ManPage = new ManPage("<n> [@users]", "`<n>` - The amount of messages to be deleted.\n\n`[@users]` - Optional argument specifying that only messages from these @users will be deleted. User mention(s) or ID(s)");
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("clearRegex");
			newCommand.Description = "Delete only messages that match a regular expression within the last `n` messages.";
			newCommand.ManPage = new ManPage("<n> <regex> [@users]", "`<n>` - The amount of messages to be deleted.\n\n`<regex>` - Regular expression based on which only the matching messages will be deleted. Do not use any whitespace in it, you can use `\\s` instead. (Note - ignores case.)\n\n`[@users]` - Optional argument specifying that only messages from these @users will be deleted. User mention(s) or ID(s)");
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("nuke");
			newCommand.Description = "Nuke the whole channel. You can also mention a user to delete all of their messages. (Within the last two weeks.)";
			newCommand.ManPage = new ManPage("[@users]", "`[@users]` - Optional argument specifying that only messages from these @users will be deleted. User mention(s) or ID(s)");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			commands.Add(newCommand);

// !op
			newCommand = new Command("op");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "_op_ yourself to be able to use `mute`, `kick` or `ban` commands. (Only if configured at <https://valkyrja.app/config>)";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				SocketRole role = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( role == null )
				{
					await e.SendReplySafe(string.Format("I'm really sorry, buuut `{0}op` feature is not configured! Poke your admin to set it up at <https://valkyrja.app/config>", e.Server.Config.CommandPrefix));
					return;
				}
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				string response = "";
				try
				{
					SocketGuildUser user = e.Message.Author as SocketGuildUser;
					if( user.Roles.Any(r => r.Id == e.Server.Config.OperatorRoleId) )
					{
						await user.RemoveRoleAsync(role);
						response = e.Server.Localisation.GetString("moderation_op_disabled");
					}
					else
					{
						await user.AddRoleAsync(role);
						response = e.Server.Localisation.GetString("moderation_op_enabled");
					}
				} catch(HttpException exception)
				{
					await e.Server.HandleHttpException(exception, $"This happened in <#{e.Channel.Id}> when executing command `{e.CommandId}`");
					response = Utils.HandleHttpException(exception);
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
				}
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !mute
			newCommand = new Command("mute");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Temporarily mute mentioned members from the chat.";
			newCommand.ManPage = new ManPage("<@users> <duration> [warning reason]", "`<@users>` - User ID(s) or mention(s) to be muted.\n\n`<duration>` - Duration with the `h` & `m` specifiers, for example `1h30m`.\n\n`[warning reason]` - An optional warning message to be recorded and PMed to the user.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				IRole role = e.Server.Guild.GetRole(e.Server.Config.MuteRoleId);
				if( role == null )
				{
					await e.SendReplySafe(RoleNotFoundString);
					return;
				}

				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				SocketRole roleOp = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( e.Server.Config.OperatorEnforce && roleOp != null && (e.Message.Author as SocketGuildUser).Roles.All(r => r.Id != roleOp.Id) &&
				    !this.Client.IsGlobalAdmin(e.Message.Author.Id) )
				{
					await e.SendReplySafe(e.Server.Localisation.GetString("moderation_op_missing", e.Server.Config.CommandPrefix));
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(BanNotFoundString);
					dbContext.Dispose();
					return;
				}

				StringBuilder warning = new StringBuilder();
				for(int i = mentionedUsers.Count + 1; i < e.MessageArgs.Length; i++)
				{
					warning.Append(e.MessageArgs[i]);
					warning.Append(" ");
				}

				if( warning.Length >= BanReasonLimit )
				{
					await e.SendReplySafe(BanReasonTooLongString);
					dbContext.Dispose();
					return;
				}

				TimeSpan? duration = Utils.GetTimespanFromString(e.MessageArgs[mentionedUsers.Count]);
				if( int.TryParse(e.MessageArgs[mentionedUsers.Count], out int muteDurationMinutes) )
					duration = TimeSpan.FromMinutes(muteDurationMinutes);
				if( duration == null || duration.Value.TotalMinutes <= 0 )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					dbContext.Dispose();
					return;
				}

				string response = "ò_ó";

				try
				{
					response = await Mute(e.Server, mentionedUsers, duration.Value, role, e.Message.Author as SocketGuildUser, warning.ToString());
					dbContext.SaveChanges();
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !unmute
			newCommand = new Command("unmute");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Unmute previously muted members.";
			newCommand.ManPage = new ManPage("<@users>", "`<@users>` - User ID(s) or mention(s) to be unmuted.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				IRole role = e.Server.Guild.GetRole(e.Server.Config.MuteRoleId);
				if( role == null || string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(MuteNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					dbContext.Dispose();
					return;
				}

				string response = "ó_ò";

				try
				{
					response = await UnMute(e.Server, mentionedUsers, role, e.Message.Author as SocketGuildUser);
					dbContext.SaveChanges();
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !muteChannel
			newCommand = new Command("muteChannel");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Temporarily mute the current channel.";
			newCommand.ManPage = new ManPage("<duration>", "`<duration>` - Duration with the `h` & `m` specifiers, for example `1h30m`.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				SocketRole roleOp = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( e.Server.Config.OperatorEnforce && roleOp != null && (e.Message.Author as SocketGuildUser).Roles.All(r => r.Id != roleOp.Id) &&
				    !this.Client.IsGlobalAdmin(e.Message.Author.Id) )
				{
					await e.SendReplySafe(e.Server.Localisation.GetString("moderation_op_missing", e.Server.Config.CommandPrefix));
					return;
				}

				string responseString = "Invalid parameters...\n" + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId);
				if( e.MessageArgs == null || e.MessageArgs.Length < 1 )
				{
					await e.SendReplySafe(responseString);
					return;
				}

				TimeSpan? duration = Utils.GetTimespanFromString(e.MessageArgs[0]);
				if( int.TryParse(e.MessageArgs[0], out int muteDurationMinutes) )
					duration = TimeSpan.FromMinutes(muteDurationMinutes);
				if( duration == null || duration.Value.TotalMinutes <= 0 )
				{
					await e.SendReplySafe(responseString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				ChannelConfig channel = dbContext.Channels.AsQueryable().FirstOrDefault(c => c.ServerId == e.Server.Id && c.ChannelId == e.Channel.Id);
				if( channel == null )
				{
					channel = new ChannelConfig{
						ServerId = e.Server.Id,
						ChannelId = e.Channel.Id
					};

					dbContext.Channels.Add(channel);
				}

				try
				{
					responseString = await MuteChannel(channel, duration.Value, e.Message.Author as SocketGuildUser);
					dbContext.SaveChanges();
				}
				catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					responseString = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}
				dbContext.Dispose();

				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);

// !unmuteChannel
			newCommand = new Command("unmuteChannel");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Unmute the current channel.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				string responseString = "";
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				ChannelConfig channel = dbContext.Channels.AsQueryable().FirstOrDefault(c => c.ServerId == e.Server.Id && c.ChannelId == e.Channel.Id);
				if( channel == null )
				{
					channel = new ChannelConfig{
						ServerId = e.Server.Id,
						ChannelId = e.Channel.Id
					};

					dbContext.Channels.Add(channel);
				}

				try
				{
					responseString = await UnmuteChannel(channel, e.Message.Author as SocketGuildUser);
					dbContext.SaveChanges();
				}
				catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					responseString = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}
				dbContext.Dispose();
				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);

// !kick
			newCommand = new Command("kick");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Kick someone, with a warning message sent to the user.";
			newCommand.ManPage = new ManPage("<@users> <warning reason>", "`<@users>` - User ID(s) or mention(s) to be kicked.\n\n`<warning reason>` - A warning message to be recorded and PMed to the user.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.KickMembers )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				SocketRole role = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( e.Server.Config.OperatorEnforce && role != null && (e.Message.Author as SocketGuildUser).Roles.All(r => r.Id != role.Id) &&
				    !this.Client.IsGlobalAdmin(e.Message.Author.Id) )
				{
					await e.SendReplySafe(e.Server.Localisation.GetString("moderation_op_missing", e.Server.Config.CommandPrefix));
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(KickArgsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(KickNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count +1 > e.MessageArgs.Length )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					dbContext.Dispose();
					return;
				}

				StringBuilder warning = new StringBuilder();
				for(int i = mentionedUsers.Count; i < e.MessageArgs.Length; i++)
				{
					warning.Append(e.MessageArgs[i]);
					warning.Append(" ");
				}

				if( warning.Length >= BanReasonLimit )
				{
					await e.SendReplySafe(BanReasonTooLongString);
					dbContext.Dispose();
					return;
				}

				string response = "";
				List<string> usernames = new List<string>();
				StringBuilder infractions = new StringBuilder();
				try
				{
					foreach( UserData userData in mentionedUsers )
					{
						IGuildUser user = e.Server.Guild.GetUser(userData.UserId);
						if( user == null )
						{
							user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(e.Server.Id, userData.UserId);
						}
						if( user == null )
							continue;

						await this.Client.SendPmSafe(user, string.Format(KickPmString, e.Server.Guild.Name, warning.ToString()));
						await Task.Delay(300);

						if( this.Client.Events.LogKick != null ) // This is in "wrong" order to prevent false positive "user left" logging.
							await this.Client.Events.LogKick(e.Server, user?.GetUsername(), userData.UserId, warning.ToString(), e.Message.Author as SocketGuildUser);

						await user.KickAsync(warning.ToString());
						userData.AddWarning(warning.ToString());
						usernames.Add(user.GetUsername());
						if( userData.WarningCount > 1 && user != null )
							infractions.AppendLine(e.Server.Localisation.GetString("moderation_nth_infraction", user.GetUsername(), userData.WarningCount));
					}
				} catch(HttpException exception)
				{
					await e.Server.HandleHttpException(exception, $"This happened in <#{e.Channel.Id}> when executing command `{e.CommandId}`");
					response = Utils.HandleHttpException(exception);
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
				}

				if( !usernames.Any() )
				{
					response = "I wasn't able to shoot anyone.";
				}
				else if( string.IsNullOrEmpty(response) )
				{
					response = e.Server.Localisation.GetString("moderation_kick_done", usernames.ToNames()) + "\n" + infractions.ToString();
					dbContext.SaveChanges();
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !ban
			newCommand = new Command("ban");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Ban someone permanently or temporarily, with a warning message sent to the user.";
			newCommand.ManPage = new ManPage("<UserIDs> <duration> <warning reason>", "`<UserIDs>` - User ID(s) to be banned.\n\n`<duration>` - Duration with the `d` & `h` specifiers, for example `1d12h`. Use `0` for permanent.\n\n`<warning reason>` - An optional warning message to be recorded and PMed to the user.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				if( !e.Server.Guild.CurrentUser.GuildPermissions.BanMembers )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				SocketRole role = e.Server.Guild.GetRole(e.Server.Config.OperatorRoleId);
				if( e.Server.Config.OperatorEnforce && role != null && ((e.Message.Author as SocketGuildUser)?.Roles.All(r => r.Id != role.Id) ?? false) &&
				    !this.Client.IsGlobalAdmin(e.Message.Author.Id) )
				{
					await e.SendReplySafe(e.Server.Localisation.GetString("moderation_op_missing", e.Server.Config.CommandPrefix));
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(BanNotFoundString);
					dbContext.Dispose();
					return;
				}

				int argOffset = string.IsNullOrWhiteSpace(e.Server.Config.BanDuration) ? 1 : 0;
				if( e.MessageArgs.Length < mentionedUsers.Count + 1 + argOffset )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					dbContext.Dispose();
					return;
				}

				TimeSpan? duration = Utils.GetTimespanFromString(e.MessageArgs[mentionedUsers.Count]);
				if( duration == null && int.TryParse(e.MessageArgs[mentionedUsers.Count], out int banDurationHours) )
					duration = TimeSpan.FromHours(banDurationHours);

				argOffset = string.IsNullOrWhiteSpace(e.Server.Config.BanDuration) || duration != null ? 1 : 0;
				if( duration == null )
					duration = Utils.GetTimespanFromString(e.Server.Config.BanDuration);

				if( duration == null )
				{
					if( string.IsNullOrWhiteSpace(e.Server.Config.BanDuration) )
						await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					else
						await e.SendReplySafe("Default ban duration is in invalid format. Pls fix.");
					dbContext.Dispose();
					return;
				}

				StringBuilder warning = new StringBuilder();
				for(int i = mentionedUsers.Count + argOffset; i < e.MessageArgs.Length; i++)
				{
					warning.Append(e.MessageArgs[i]);
					warning.Append(" ");
				}

				string response = "ò_ó";

				if( warning.Length >= BanReasonLimit )
				{
					await e.SendReplySafe(BanReasonTooLongString);
					dbContext.Dispose();
					return;
				}

				try
				{
					response = await Ban(e.Server, mentionedUsers, duration.Value, warning.ToString(), e.Message.Author as SocketGuildUser,
						e.Command.Id.ToLower() == "silentban", e.Command.Id.ToLower() == "purgeban", e.Command.Id.ToLower() == "redactedban");
					dbContext.SaveChanges();
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("silentBan");
			newCommand.Description = "Ban someone where the warning message will not be sent to the user (hence silent.)";
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("purgeBan");
			newCommand.Description = "Ban someone just like the usual, but also delete all of their messages within the last 24 hours.";
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("redactedBan");
			newCommand.Description = "Ban someone just like the usual, but do not log their user ID or #discrim.";
			commands.Add(newCommand);

// !quickBan
			newCommand = new Command("quickBan");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Quickly ban someone using pre-configured reason and duration, it also removes their messages. (This command has to be first configured via `config` or <https://valkyrja.app/config>.)";
			newCommand.ManPage = new ManPage("<UserIDs>", "`<UserIDs>` - User ID(s) or mention(s) to be banned.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				if( string.IsNullOrEmpty(e.Server.Config.QuickbanReason) )
				{
					await e.SendReplySafe("This command has to be first configured via `config` or <https://valkyrja.app/config>.");
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(BanNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
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
					await this.Client.LogException(exception, e);
					response = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !unBan
			newCommand = new Command("unBan");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Unban people. You may need to use the ID instead of a mention, since they're not on the server and you can't mention them!";
			newCommand.ManPage = new ManPage("<UserIDs>", "`<UserIDs>` - User ID(s) to be unbanned.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(BanNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					dbContext.Dispose();
					return;
				}

				string response = "ó_ò";

				try
				{
					response = await UnBan(e.Server, mentionedUsers, e.Message.Author as SocketGuildUser);
					dbContext.SaveChanges();
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !addWarning
			newCommand = new Command("addWarning");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Adds a warning note to the database for a specific user.";
			newCommand.ManPage = new ManPage("<UserIDs> <warning>", "`<UserIDs>` - User ID(s) or mention(s) to have a warning recorded.\n\n`<warning>` - A warning message to be recorded.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe("Give a warning to whom?\n" + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(WarningNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count + 1 > e.MessageArgs.Length )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
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
				List<UserData> failedToPmUsers = new List<UserData>();
				List<string> userNames = new List<string>();
				List<guid> userIds = new List<guid>();
				StringBuilder infractions = new StringBuilder();
				foreach(UserData userData in mentionedUsers)
				{
					IGuildUser user = e.Server.Guild.GetUser(userData.UserId);
					if( user == null )
					{
						user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(e.Server.Id, userData.UserId);
					}
					userNames.Add(user?.GetUsername() ?? "<unknown>");
					userIds.Add(userData.UserId);
					userData.AddWarning(warning.ToString());
					if( userData.WarningCount > 1 && user != null )
						infractions.AppendLine(e.Server.Localisation.GetString("moderation_nth_infraction", user.GetUsername(), userData.WarningCount));

					if( sendMessage )
					{
						if( user == null || PmErrorCode.Success != await this.Client.SendPmSafe(user, string.Format(WarningPmString, e.Server.Guild.Name, warning.ToString())) )
							failedToPmUsers.Add(userData);
					}
				}

				if( this.Client.Events.LogWarning != null )
					await this.Client.Events.LogWarning(e.Server, userNames, userIds, warning.ToString(), e.Message.Author as SocketGuildUser);

				string response = "Done.\n" + infractions.ToString();
				if( failedToPmUsers.Any() )
					response = "Failed to PM " + failedToPmUsers.Select(u => u.UserId).ToMentions();

				dbContext.SaveChanges();

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("issueWarning");
			newCommand.Description = "Adds a warning note to the database, and also PMs it to the user.";
			newCommand.Description = "Use with parameters `@user warning` where `@user` = user mention or id, you can add the same warning to multiple people, just mention them all; `warning` = worded description, a warning message to store in the database. This will also be PMed to the user.";
			newCommand.ManPage = new ManPage("<UserIDs> <warning>", "`<UserIDs>` - User ID(s) or mention(s) to have a warning recorded.\n\n`<warning>` - A warning message to be recorded and PMed to the user.");
			commands.Add(newCommand);

// !removeWarning
			newCommand = new Command("removeWarning");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Removes the last recorded warning from a specific user(s).";
			newCommand.ManPage = new ManPage("<UserIDs>", "`<UserIDs>` - User ID(s) or mention(s) to have a warning recorded.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe("Remove warning from whom?\n" + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);

				if( mentionedUsers.Count == 0 )
				{
					await e.SendReplySafe(WarningNotFoundString);
					dbContext.Dispose();
					return;
				}

				if( mentionedUsers.Count < e.MessageArgs.Length )
				{
					await e.SendReplySafe(InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId));
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
				await e.SendReplySafe("Done.");
			};
			commands.Add(newCommand);

			newCommand = newCommand.CreateCopy("removeAllWarnings");
			newCommand.Description = "Use with parameter `@user` = user mention or id, you can remove all the warnings from multiple people, just mention them all.";
			commands.Add(newCommand);

// !warnings
			newCommand = new Command("warnings");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display your own warnings.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);
				string response = userData.GetWarningsString();
				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("infractions"));

// !listWarnedUsers
			newCommand = new Command("listWarnedUsers");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display a list of users with more than specific amount of warnings. (Ignores permanently banned.)";
			newCommand.ManPage = new ManPage("<n>", "`<n>` - Threshold above which a user will be added to the output.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				int n = 0;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !int.TryParse(e.TrimmedMessage, out n) || n < 1 )
				{
					await e.SendReplySafe("How many?");
					return;
				}

				if( e.Server?.Guild?.Users == null )
				{
					await e.SendReplySafe("Encountered unexpected D.Net library error.");
					return;
				}

				string response = "I found too many, please be more specific.";
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> foundUserData = dbContext.UserDatabase.AsQueryable().Where(u => u.ServerId == e.Server.Id && u.WarningCount >= n).ToList();

				if( foundUserData.Count <= 50 )
				{
					StringBuilder responseBuilder = new StringBuilder("`UserId` | `n` | `UserName`\n");
					foreach( UserData userData in foundUserData.OrderByDescending(u => u.WarningCount) )
					{
						if( userData.BannedUntil > DateTime.UtcNow + TimeSpan.FromDays(1000) )
							continue;
						IGuildUser user = e.Server.Guild.GetUser(userData.UserId);
						if( user == null )
						{
							user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(e.Server.Id, userData.UserId);
						}
						responseBuilder.AppendLine($"`{userData.UserId}` | `{userData.WarningCount}` | `{(user?.GetUsername() ?? "<User Not Present>")}`");
					}

					response = responseBuilder.ToString();
					if( string.IsNullOrEmpty(response) )
					{
						response = "I did not find anyone.";
					}
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !listIDs
			newCommand = new Command("listIDs");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Search and display all User IDs on this server that match a username expression.";
			newCommand.ManPage = new ManPage("<expression>", "`<expression>` - User IDs with a username or a nickname matching this expression will be returned.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe("Who are you looking for?");
					return;
				}

				if( e.Server?.Guild?.Users == null )
				{
					await e.SendReplySafe("Encountered unexpected D.Net library error.");
					return;
				}

				string expression = e.TrimmedMessage.ToLower();
				List<guid> foundUserIds = e.Server.Guild.Users
					.Where(u => u != null && ((u.Username != null && u.Username.ToLower().Contains(expression)) ||
					            (u.Nickname != null && u.Nickname.ToLower().Contains(expression))))
					.Select(u => u.Id).ToList();

				StringBuilder idStrings = new StringBuilder();
				if( foundUserIds.Any() )
				{
					foreach( guid id in foundUserIds )
					{
						idStrings.AppendLine($"`{id}`");
					}
				}

				string response = idStrings.ToString();
				if( string.IsNullOrEmpty(response) )
				{
					response = "I did not find anyone.";
				}

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !whois
			newCommand = new Command("whois");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Search for a User on this server and display all the things we know about them, including past warnings, bans, etc.";
			newCommand.ManPage = new ManPage("<UserID | expression>", "`<UserID>` - User ID or mention to look for.\n\n`<expression>` - A user with a username or a nickname matching this expression will be returned.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe("Who are you looking for?");
					return;
				}

				if( e.Server?.Guild?.Users == null )
				{
					await e.SendReplySafe("Encountered unexpected D.Net library error.");
					return;
				}

				string expression = e.TrimmedMessage.ToLower();
				List<guid> foundUserIds = e.Server.Guild.Users
					.Where(u => u != null && ((u.Username != null && u.Username.ToLower().Contains(expression)) ||
					            (u.Nickname != null && u.Nickname.ToLower().Contains(expression))))
					.Select(u => u.Id).ToList();

				foundUserIds.AddRange(e.Message.MentionedUsers.Select(u => u.Id));

				for( int i = 0; i < e.MessageArgs.Length; i++ )
				{
					if( guid.TryParse(e.MessageArgs[i], out guid id) )
						foundUserIds.Add(id);
				}

				string response = "I found too many, please be more specific.";
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

				if( foundUserIds.Count <= 5 )
				{
					StringBuilder whoisStrings = new StringBuilder();
					for( int i = 0; i < foundUserIds.Count; i++ )
					{
						UserData userData = dbContext.GetOrAddUser(e.Server.Id, foundUserIds[i]);
						if( userData != null )
						{
							IGuildUser user = e.Server.Guild.GetUser(foundUserIds[i]);
							if( user == null )
								user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(e.Server.Id, userData.UserId);

							whoisStrings.AppendLine(userData.GetWhoisString(dbContext, user));
						}
					}

					response = whoisStrings.ToString();
					if( string.IsNullOrEmpty(response) )
					{
						response = "I did not find anyone.";
					}
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !find
			newCommand = new Command("find");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Search for a User in the database and display all the things we know about them, including past warnings, bans, etc.";
			newCommand.ManPage = new ManPage("<UserID | expression>", "`<UserID>` - User ID or mention to look for.\n\n`<expression>` - A user with any current or past username or nickname matching this expression will be returned.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe("Who are you looking for?");
					return;
				}

				string response = "I found too many, please be more specific.";
				string expression = e.TrimmedMessage.ToLower();
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<Username> foundUsernames = dbContext.Usernames.AsQueryable().Where(u => u.ServerId == e.Server.Id).AsEnumerable().Where(u => u.Name.ToLower().Contains(expression)).ToList();
				List<Nickname> foundNicknames = dbContext.Nicknames.AsQueryable().Where(u => u.ServerId == e.Server.Id).AsEnumerable().Where(u => u.Name.ToLower().Contains(expression)).ToList();
				List<guid> foundUserIds = new List<guid>();

				for( int i = 0; i < e.MessageArgs.Length; i++ )
				{
					if( guid.TryParse(e.MessageArgs[i].Trim('<', '@', '!', '>'), out guid id) )
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
						UserData userData = dbContext.GetOrAddUser(e.Server.Id, foundUserIds[i]);
						if( userData != null )
						{
							IGuildUser user = e.Server.Guild.GetUser(foundUserIds[i]);
							if( user == null )
								user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(e.Server.Id, userData.UserId);

							if( e.Command.Id == "names" )
								whoisStrings.AppendLine(userData.GetNamesString(dbContext, user));
							else
								whoisStrings.AppendLine(userData.GetWhoisString(dbContext, user));
						}
					}

					response = whoisStrings.ToString();
				}

				if( string.IsNullOrEmpty(response) )
				{
					response = "I did not find anyone.";
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("names");
			newCommand.Description = "Find a User and display all their usernames and nicknames.";
			commands.Add(newCommand);

// !slow
			newCommand = new Command("slow");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Enable or disable slowmode in the current channel.";
			newCommand.ManPage = new ManPage("[n]", "`[n]` - A number specifying the time interval between two messages in seconds (or use time quantifiers `s`, `m`, `h` or `d` - e.g. `1m30s`.) Defaults to 60m.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				string response = "";
				int seconds = 0;
				TimeSpan? interval = null;

				if( e.MessageArgs == null || e.MessageArgs.Length < 1 || int.TryParse(e.MessageArgs[0], out seconds) || (interval = Utils.GetTimespanFromString(e.MessageArgs[0])) != null )
				{
					if( seconds >= 21600 || (interval.HasValue && (int)interval.Value.TotalSeconds >= 21600) )
					{
						seconds = 21600;
						response = $"Y'all can now send one message every 6 hours. (Yes that is the maximum.)";
					}
					else if( interval.HasValue && (int)interval.Value.TotalSeconds < 21600 )
					{
						seconds = (int)interval.Value.TotalSeconds;
						response = "Y'all can now send one message every" + interval.Value.ToFancyString();
					}
					else if( seconds > 0 && seconds < 21600 )
					{
						response = "Y'all can now send one message every" + TimeSpan.FromSeconds(seconds).ToFancyString();
					}
					else
					{
						seconds = 0;
						if( e.Channel.SlowModeInterval > 0 )
							response = "Slowmode disabled.";
						else
						{
							seconds = e.Server.Config.SlowmodeDefaultSeconds;
							response = "Y'all can now send one message every" + TimeSpan.FromSeconds(seconds).ToFancyString();
						}
					}

					await e.Channel.ModifyAsync(c => c.SlowModeInterval = seconds);
				}
				else
					response = InvalidArgumentsString + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix+e.CommandId);

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

			return commands;
		}

//User Joined
		private async Task OnUserJoined(SocketGuildUser user)
		{
			IRole role;
			Server server;
			if( !this.Client.Servers.ContainsKey(user.Guild.Id) ||
			    (server = this.Client.Servers[user.Guild.Id]) == null ||
			    (role = server.Guild.GetRole(server.Config.MuteRoleId)) == null )
				return;

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
			UserData userData = dbContext.GetOrAddUser(user.Guild.Id, user.Id);

			if( userData.Muted && userData.MutedUntil > DateTime.MinValue + TimeSpan.FromMinutes(1) )
			{
				try
				{
					if( userData.MutedUntil < DateTime.UtcNow )
					{
						await UnMute(server, new List<UserData>{userData}, role, server.Guild.CurrentUser);
						dbContext.SaveChanges();
					}
					else
						await user.AddRoleAsync(role);
				}
				catch( HttpException ex )
				{
					await server.HandleHttpException(ex, $"Failed to mute or unmute a user.");
				}
				catch( Exception ex )
				{
					await this.HandleException(ex, "Moderation - OnUserJoined - mute/unmute", server.Id);
				}
			}

			dbContext.Dispose();
		}


//Update
		public async Task Update(IValkyrjaClient iClient)
		{
			ValkyrjaClient client = iClient as ValkyrjaClient;
			ServerContext dbContext = ServerContext.Create(client.DbConnectionString);
			bool save = false;
			DateTime minTime = DateTime.MinValue + TimeSpan.FromMinutes(1);

			this.JustBannedId = 0;

			//Channels
			foreach( ChannelConfig channelConfig in dbContext.Channels.AsQueryable().Where(c => c.Muted) )
			{
				Server server;
				if( !client.Servers.ContainsKey(channelConfig.ServerId) ||
				    (server = client.Servers[channelConfig.ServerId]) == null )
					continue;

				//Muted channels
				if( channelConfig.Muted && channelConfig.MutedUntil > minTime && channelConfig.MutedUntil < DateTime.UtcNow )
				{
					await UnmuteChannel(channelConfig, client.DiscordClient.CurrentUser);
					save = true;
				}
			}

			//Users
			foreach( UserData userData in dbContext.UserDatabase.AsQueryable().Where(ud => ud.Banned || ud.Muted) )
			{
				Server server = null;
				try
				{
					//Unban
					if( userData.Banned && userData.BannedUntil > minTime && userData.BannedUntil < DateTime.UtcNow &&
					    client.Servers.ContainsKey(userData.ServerId) && (server = client.Servers[userData.ServerId]) != null )
					{
						await UnBan(server, new List<UserData>{userData}, server.Guild.CurrentUser);
						save = true;

						//else: ban them if they're on the server - implement if the ID pre-ban doesn't work.
					}

					//Unmute
					IRole role;
					if( userData.Muted && userData.MutedUntil > minTime && userData.MutedUntil < DateTime.UtcNow &&
					    client.Servers.ContainsKey(userData.ServerId) && (server = client.Servers[userData.ServerId]) != null &&
					    (role = server.Guild.GetRole(server.Config.MuteRoleId)) != null )
					{
						await UnMute(server, new List<UserData>{userData}, role, server.Guild.CurrentUser);
						save = true;
					}
				}
				catch(HttpException ex)
				{
					if( server != null )
						await server.HandleHttpException(ex, "While trying to unban or unmute someone");
				}
				catch(Exception ex)
				{
					await this.HandleException(ex, "Update Moderation", userData.ServerId);
				}
			}

			if( save )
				dbContext.SaveChanges();
			dbContext.Dispose();
		}


// Ban

		public Task AddBan(guid serverid, guid userid, TimeSpan duration, string reason)
		{
			DateTime bannedUntil = DateTime.MaxValue;
			if( duration.TotalHours >= 1 )
				bannedUntil = DateTime.UtcNow + duration;
			else
				duration = TimeSpan.Zero;

			string durationString = Utils.GetDurationString(duration);
			string logMessage = $"Banned {durationString.ToString()} with reason: {reason.Replace("@everyone", "@-everyone").Replace("@here", "@-here")}";

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

			UserData userData = dbContext.GetOrAddUser(serverid, userid);
			userData.BannedUntil = bannedUntil;
			userData.Banned = true;
			userData.AddWarning(logMessage);

			dbContext.SaveChanges();
			dbContext.Dispose();
			return Task.CompletedTask;
		}

		/// <summary> Ban the User - this will also ban them as soon as they join the server, if they are not there right now. </summary>
		/// <param name="duration">Use zero for permanent ban.</param>
		/// <param name="silent">Set to true to not PM the user information about the ban (time, server, reason)</param>
		public async Task<string> Ban(Server server, List<UserData> users, TimeSpan duration, string reason, SocketGuildUser bannedBy = null, bool silent = false, bool deleteMessages = false, bool redacted = false)
		{
			DateTime bannedUntil = DateTime.MaxValue;
			if( duration.TotalHours >= 1 )
				bannedUntil = DateTime.UtcNow + duration;
			else
				duration = TimeSpan.Zero;

			string durationString = Utils.GetDurationString(duration);

			string response = "";
			List<guid> banned = new List<guid>();
			StringBuilder infractions = new StringBuilder();
			foreach( UserData userData in users )
			{
				SocketGuildUser user = null;
				if( !silent && (user = server.Guild.GetUser(userData.UserId)) != null )
				{
					await this.Client.SendPmSafe(user, string.Format(BanPmString, durationString.ToString(), server.Guild.Name, reason));
					await Task.Delay(500);
				}

				try
				{
					Task logBan = null;
					if( this.Client.Events.LogBan != null ) // This is in "wrong" order to prevent false positive "user left" logging.
						logBan = this.Client.Events.LogBan(server, redacted ? user?.Username + "#redacted" : user?.GetUsername(), userData.UserId, reason, durationString.ToString(), bannedBy);

					string logMessage = $"Banned {durationString.ToString()} with reason: {reason.Replace("@everyone", "@-everyone").Replace("@here", "@-here")}";
					await server.Guild.AddBanAsync(userData.UserId, (deleteMessages ? 1 : 0), (bannedBy?.GetUsername() ?? "") + " " + logMessage);
					userData.BannedUntil = bannedUntil;
					userData.Banned = true;
					userData.AddWarning(logMessage);
					banned.Add(userData.UserId);
					if( userData.WarningCount > 1 && user != null )
						infractions.AppendLine(server.Localisation.GetString("moderation_nth_infraction", user.GetUsername(), userData.WarningCount));
					if( logBan != null )
						await logBan;
				} catch(HttpException exception)
				{
					await server.HandleHttpException(exception, $"This happened in when trying to ban <@{userData.UserId}>");
					response = Utils.HandleHttpException(exception);
				}
			}

			if( banned.Any() )
				response = server.Localisation.GetString("moderation_ban_done", banned.ToMentions()) + "\n" + infractions.ToString();

			return response;
		}

		public async Task Ban(Server server, UserData userData, TimeSpan duration, string reason, SocketGuildUser bannedBy = null, bool silent = false, bool deleteMessages = false)
		{
			DateTime bannedUntil = DateTime.MaxValue;
			if( duration.TotalHours >= 1 )
				bannedUntil = DateTime.UtcNow + duration;
			else
				duration = TimeSpan.Zero;

			string durationString = Utils.GetDurationString(duration);

			SocketGuildUser user = null;
			if( !silent && (user = server.Guild.GetUser(userData.UserId)) != null )
			{
				await this.Client.SendPmSafe(user, string.Format(BanPmString, durationString.ToString(), server.Guild.Name, reason));
				await Task.Delay(500);
			}

			Task logBan = null;
			if( this.Client.Events.LogBan != null && userData.UserId != this.JustBannedId )
			{
				this.JustBannedId = userData.UserId;
				logBan = this.Client.Events.LogBan(server, user?.GetUsername(), userData.UserId, reason, durationString, bannedBy);
			}

			await server.Guild.AddBanAsync(userData.UserId, (deleteMessages ? 1 : 0), reason);
			userData.BannedUntil = bannedUntil;
			userData.Banned = true;
			userData.AddWarning($"Banned {durationString} with reason: {reason}");
			if( logBan != null )
				await logBan;
		}

		public async Task<string> UnBan(Server server, List<UserData> users, SocketGuildUser unbannedBy = null)
		{
			string response = "";
			List<guid> unbanned = new List<guid>();
			foreach( UserData userData in users )
			{
				try
				{
					await server.Guild.RemoveBanAsync(userData.UserId);
					unbanned.Add(userData.UserId);
					userData.BannedUntil = DateTime.MinValue;
					userData.Banned = false;

					if( this.Client.Events.LogUnban != null )
						await this.Client.Events.LogUnban(server, userData.LastUsername, userData.UserId, unbannedBy);
				} catch(HttpException exception)
				{
					await server.HandleHttpException(exception, $"This happened in when trying to unban <@{userData.UserId}>");
					response = Utils.HandleHttpException(exception);
				}
			}

			if( unbanned.Any() )
				response = string.Format(UnbanConfirmString, unbanned.ToMentions());

			return response;
		}


// Mute
		public async Task Mute(Server server, UserData userData, TimeSpan duration, IRole role, SocketGuildUser mutedBy = null)
		{
			await Mute(server, userData, duration, role, mutedBy, null);
		}

		public async Task Mute(Server server, UserData userData, TimeSpan duration, IRole role, SocketGuildUser mutedBy = null, string reason = null)
		{
			if( userData.UserId == this.Client.DiscordClient.CurrentUser.Id )
			{
				return;
			}

			DateTime mutedUntil = DateTime.UtcNow + (duration.TotalMinutes < 5 ? TimeSpan.FromMinutes(5) : duration);
			string durationString = Utils.GetDurationString(duration);

			IGuildUser user = server.Guild.GetUser(userData.UserId);
			if( user == null )
			{
				user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(server.Id, userData.UserId);
			}
			await user.AddRoleAsync(role);

			string warning = $"Muted {durationString}";
			if( !string.IsNullOrEmpty(reason) )
				warning += $" with reason: {reason}";
			userData.AddWarning(warning);
			userData.MutedUntil = mutedUntil;
			userData.Muted = true;

			SocketTextChannel logChannel;
			if( (logChannel = server.Guild.GetTextChannel(server.Config.MuteIgnoreChannelId)) != null )
				await logChannel.SendMessageSafe(string.Format(MuteIgnoreChannelString, $"<@{userData.UserId}>"));

			if( this.Client.Events.LogMute != null )
				await this.Client.Events.LogMute(server, user, durationString, mutedBy);
		}

		public async Task<string> Mute(Server server, List<UserData> users, TimeSpan duration, IRole role, SocketGuildUser mutedBy = null)
		{
			return await Mute(server, users, duration, role, mutedBy, null);
		}

		public async Task<string> Mute(Server server, List<UserData> users, TimeSpan duration, IRole role, SocketGuildUser mutedBy = null, string reason = null)
		{
			DateTime mutedUntil = DateTime.UtcNow + (duration.TotalMinutes < 5 ? TimeSpan.FromMinutes(5) : duration);
			string durationString = Utils.GetDurationString(duration);

			string response = "";
			List<guid> muted = new List<guid>();
			StringBuilder infractions = new StringBuilder();
			foreach( UserData userData in users )
			{
				try
				{
					if( userData.UserId == this.Client.DiscordClient.CurrentUser.Id )
					{
						response = "But.... no? >_<";
						continue;
					}

					IGuildUser user = server.Guild.GetUser(userData.UserId);
					if( user == null )
					{
						user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(server.Id, userData.UserId);
					}
					if( user == null )
					{
						response = NotFoundString;
						continue;
					}

					await user.AddRoleAsync(role);

					string warning = $"Muted {durationString}";
					if( !string.IsNullOrEmpty(reason) )
						warning += $" with reason: {reason}";
					userData.AddWarning(warning);
					userData.MutedUntil = mutedUntil;
					userData.Muted = true;
					muted.Add(userData.UserId);
					if( userData.WarningCount > 1 && user != null )
						infractions.AppendLine(server.Localisation.GetString("moderation_nth_infraction", user.GetUsername(), userData.WarningCount));

					if( this.Client.Events.LogMute != null )
						await this.Client.Events.LogMute(server, user, durationString, mutedBy);

					await this.Client.SendPmSafe(user, $"You were {warning}\n{(server.Config.MuteMessage ?? "")}");
				} catch(HttpException exception)
				{
					await server.HandleHttpException(exception, $"This happened in when trying to mute <@{userData.UserId}>");
					response = Utils.HandleHttpException(exception);
				}
			}

			string mentions = muted.ToMentions();
			if( muted.Any() )
			{
				response = server.Localisation.GetString("moderation_mute_done", mentions) + "\n" + infractions.ToString();

				SocketTextChannel logChannel;
				if( (logChannel = server.Guild.GetTextChannel(server.Config.MuteIgnoreChannelId)) != null )
					await logChannel.SendMessageSafe(string.Format(MuteIgnoreChannelString, mentions));
			}

			return response;
		}

		public async Task<string> UnMute(Server server, List<UserData> users, IRole role, SocketGuildUser unmutedBy = null)
		{
			string response = "";
			List<guid> unmuted = new List<guid>();
			foreach( UserData userData in users )
			{
				try
				{
					IGuildUser user = server.Guild.GetUser(userData.UserId);
					if( user == null )
					{
						user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(server.Id, userData.UserId);
					}
					if( user == null )
					{
						response = NotFoundString;
						continue;
					}

					if( user.RoleIds.Any(rId => rId == role.Id) )
						await user.RemoveRoleAsync(role);
					unmuted.Add(userData.UserId);
					userData.MutedUntil = DateTime.MinValue;
					userData.Muted = false;

					if( this.Client.Events.LogUnmute != null )
						await this.Client.Events.LogUnmute(server, user, unmutedBy);
				} catch(HttpException exception)
				{
					await server.HandleHttpException(exception, $"This happened in when trying to unmute <@{userData.UserId}>");
					response = Utils.HandleHttpException(exception);
				}
			}

			if( unmuted.Any() )
				response = string.Format(UnmuteConfirmString, unmuted.ToMentions());

			return response;
		}

		public async Task<string> MuteChannel(ChannelConfig channelConfig, TimeSpan duration, SocketGuildUser mutedBy = null)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(channelConfig.ServerId) || (server = this.Client.Servers[channelConfig.ServerId]) == null )
				throw new Exception("Server not found.");

			string response = MuteChannelConfirmString;
			try
			{
				IRole role = server.Guild.EveryoneRole;
				SocketGuildChannel channel = server.Guild.GetChannel(channelConfig.ChannelId);
				OverwritePermissions permissions = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
				permissions = permissions.Modify(sendMessages: PermValue.Deny);
				await channel.AddPermissionOverwriteAsync(role, permissions);

				channelConfig.MutedUntil = DateTime.UtcNow + duration;
				channelConfig.Muted = true;

				if( this.Client.Events.LogMutedChannel != null )
					await this.Client.Events.LogMutedChannel(server, channel, Utils.GetDurationString(duration), mutedBy);
			} catch(HttpException exception)
			{
				await server.HandleHttpException(exception, $"This happened in <#{channelConfig.ChannelId}> when muting the channel (modifying its permissions)");
				response = Utils.HandleHttpException(exception);
			}
			return response;
		}

		public async Task<string> UnmuteChannel(ChannelConfig channelConfig, SocketUser unmutedBy = null)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(channelConfig.ServerId) || (server = this.Client.Servers[channelConfig.ServerId]) == null )
				throw new Exception("Server not found.");

			string response = UnmuteChannelConfirmString;
			try
			{
				channelConfig.MutedUntil = DateTime.MinValue;
				channelConfig.Muted = false;

				IRole role = server.Guild.EveryoneRole;
				SocketGuildChannel channel = server.Guild.GetChannel(channelConfig.ChannelId);
				if( channel == null )
					return NotFoundChannelString;

				OverwritePermissions permissions = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
				permissions = permissions.Modify(sendMessages: PermValue.Inherit);
				await channel.AddPermissionOverwriteAsync(role, permissions);

				if( this.Client.Events.LogUnmutedChannel != null )
					await this.Client.Events.LogUnmutedChannel(server, channel, unmutedBy);
			} catch(HttpException exception)
			{
				await server.HandleHttpException(exception, $"This happened in <#{channelConfig.ChannelId}> when unmuting the channel (modifying its permissions)");
				response = Utils.HandleHttpException(exception);
			}
			return response;
		}
	}
}
