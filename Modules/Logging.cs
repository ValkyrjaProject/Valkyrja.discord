using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;

using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Logging: IModule
	{
		private BotwinderClient Client;

		private readonly List<guid> RecentlyBannedUserIDs = new List<guid>();
		private readonly List<guid> RecentlyUnbannedUserIDs = new List<guid>();

		private readonly TimeSpan UpdateDelay = TimeSpan.FromMinutes(2);
		private DateTime LastUpdateTime = DateTime.UtcNow;

		private readonly Color AntispamColor = new Color(255, 0, 255);
		private readonly Color AntispamLightColor = new Color(255, 0, 206);


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;

			this.Client.Events.UserJoined += OnUserJoined;
			this.Client.Events.UserLeft += OnUserLeft;
			this.Client.Events.UserVoiceStateUpdated += OnUserVoice;
			this.Client.Events.MessageDeleted += OnMessageDeleted;
			this.Client.Events.MessageUpdated += OnMessageUpdated;
			this.Client.Events.UserBanned += OnUserBanned;
			this.Client.Events.UserUnbanned += async (user, guild) => {
				Server server;
				if( !this.Client.Servers.ContainsKey(guild.Id) || (server = this.Client.Servers[guild.Id]) == null ||
				    this.RecentlyUnbannedUserIDs.Contains(user.Id) )
					return;

				await LogUnban(server, user.GetUsername(), user.Id, null);
			};

			this.Client.Events.LogBan += LogBan;
			this.Client.Events.LogUnban += LogUnban;
			this.Client.Events.LogKick += LogKick;
			this.Client.Events.LogMute += LogMute;
			this.Client.Events.LogUnmute += LogUnmute;
			this.Client.Events.LogMutedChannel += LogMutedChannel;
			this.Client.Events.LogUnmutedChannel += LogUnmutedChannel;

			this.Client.Events.LogPublicRoleJoin += LogPublicRoleJoin;
			this.Client.Events.LogPublicRoleLeave += LogPublicRoleLeave;
			this.Client.Events.LogPromote += LogPromote;
			this.Client.Events.LogDemote += LogDemote;

			return new List<Command>();
		}

		private async Task OnUserJoined(SocketGuildUser user)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(user.Guild.Id) ||
				(server = this.Client.Servers[user.Guild.Id]) == null )
				return;

			try
			{
				SocketTextChannel channel = server.Guild.GetTextChannel(server.Config.ActivityChannelId);
				if( server.Config.LogJoin && channel != null && !string.IsNullOrWhiteSpace(server.Config.LogMessageJoin) )
				{
					string joinMessage = string.Format(server.Config.LogMessageJoin, user.GetUsername());
					if( server.Config.ActivityChannelEmbeds && joinMessage.Length < 255 )
					{
						DateTime accountCreated = Utils.GetTimeFromId(user.Id);
						await channel.SendMessageAsync("", embed:
							GetLogSmolEmbed(new Color(server.Config.ActivityChannelColor),
								joinMessage,
								user.GetAvatarUrl(), $"UserId: {user.Id}",
								"Account created: " + Utils.GetTimestamp(accountCreated), accountCreated));
					}
					else
					{
						await this.Client.SendRawMessageToChannel(channel,
							string.Format((server.Config.LogTimestampJoin ? $"`{Utils.GetTimestamp()}`: " : "") + server.Config.LogMessageJoin,
								server.Config.LogMentionJoin ? $"<@{user.Id}>" : $"**{user.GetNickname()}**")
								.Replace("@everyone", "@-everyone").Replace("@here", "@-here"));
					}
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "OnUserJoined", server.Id);
			}
		}

		private async Task OnUserLeft(SocketGuildUser user)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(user.Guild.Id) ||
			    (server = this.Client.Servers[user.Guild.Id]) == null||
			    this.RecentlyBannedUserIDs.Contains(user.Id) )
				return;

			try
			{
				SocketTextChannel channel = server.Guild.GetTextChannel(server.Config.ActivityChannelId);
				if( server.Config.LogLeave && channel != null && !string.IsNullOrWhiteSpace(server.Config.LogMessageLeave) )
				{
					string leaveMessage = string.Format(server.Config.LogMessageLeave, user.GetUsername());
					if( server.Config.ActivityChannelEmbeds && leaveMessage.Length < 255 )
					{
						DateTime accountCreated = Utils.GetTimeFromId(user.Id);
						await channel.SendMessageAsync("", embed:
							GetLogSmolEmbed(new Color(server.Config.ActivityChannelColor),
								leaveMessage,
								user.GetAvatarUrl(), $"UserId: {user.Id}",
								"Account created: " + Utils.GetTimestamp(accountCreated), accountCreated));
					}
					else
					{
						await this.Client.SendRawMessageToChannel(channel,
							string.Format((server.Config.LogTimestampLeave ? $"`{Utils.GetTimestamp()}`: " : "") + server.Config.LogMessageLeave,
								server.Config.LogMentionLeave ? $"<@{user.Id}>" : $"**{user.GetNickname()}**")
								.Replace("@everyone", "@-everyone").Replace("@here", "@-here"));
					}
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "OnUserLeft", server.Id);
			}

		}

		private async Task OnUserVoice(SocketUser u, SocketVoiceState originalState, SocketVoiceState newState)
		{
			Server server;
			guid id = newState.VoiceChannel?.Guild.Id ?? originalState.VoiceChannel?.Guild.Id ?? 0;
			if( id == 0 ||
			    !this.Client.Servers.ContainsKey(id) ||
			    (server = this.Client.Servers[id]) == null ||
			    !(u is SocketGuildUser user) ||
			    originalState.VoiceChannel == newState.VoiceChannel )
				return;

			try
			{
				SocketTextChannel channel;
				if( server.Config.VoiceChannelId != 0 &&
				    (channel = server.Guild.GetTextChannel(server.Config.VoiceChannelId)) != null )
				{
					if( originalState.VoiceChannel == null && newState.VoiceChannel == null )
						throw new ArgumentNullException("Logging.VoiceState.VoiceChannel(s) are null.");

					int change = originalState.VoiceChannel == null ? 1 :
						newState.VoiceChannel == null ? -1 : 0;

					if( server.Config.VoiceChannelEmbeds )
					{
						switch(change)
						{
							case -1:
								await channel.SendMessageAsync("", embed:
									GetLogSmolEmbed(new Color(server.Config.VoiceChannelColor),
										user.GetUsername() + " left the voice channel:",
										user.GetAvatarUrl(), $"{originalState.VoiceChannel.Name}",
										Utils.GetTimestamp(DateTime.UtcNow), DateTime.UtcNow));
								break;
							case 1:
								await channel.SendMessageAsync("", embed:
									GetLogSmolEmbed(new Color(server.Config.VoiceChannelColor),
										user.GetUsername() + " joined the voice channel:",
										user.GetAvatarUrl(), $"{newState.VoiceChannel.Name}",
										Utils.GetTimestamp(DateTime.UtcNow), DateTime.UtcNow));
								break;
							case 0:
								await channel.SendMessageAsync("", embed:
									GetLogSmolEmbed(new Color(server.Config.VoiceChannelColor),
										user.GetUsername() + " switched voice channels:",
										user.GetAvatarUrl(), $"From {originalState.VoiceChannel.Name} to {newState.VoiceChannel.Name}",
										Utils.GetTimestamp(DateTime.UtcNow), DateTime.UtcNow));
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}
						return;
					}

					string message = "";
					switch(change)
					{
						case -1:
							message = $"`{Utils.GetTimestamp()}`:  **{user.GetNickname()}** left the `{originalState.VoiceChannel.Name}` voice channel.";
							break;
						case 1:
							message = $"`{Utils.GetTimestamp()}`:  **{user.GetNickname()}** joined the `{newState.VoiceChannel.Name}` voice channel.";
							break;
						case 0:
							message = $"`{Utils.GetTimestamp()}`:  **{user.GetNickname()}** switched from the `{originalState.VoiceChannel.Name}` voice channel, to the `{newState.VoiceChannel.Name}` voice channel.";
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					await channel.SendMessageSafe(message);
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "OnUserVoice", server.Id);
			}

		}

		private async Task OnMessageDeleted(SocketMessage message, ISocketMessageChannel c)
		{
			if( !(c is SocketTextChannel channel) )
				return;

			Server server;
			if( !this.Client.Servers.ContainsKey(channel.Guild.Id) ||
			    (server = this.Client.Servers[channel.Guild.Id]) == null ||
			    server.Config.IgnoreBots && message.Author.IsBot ||
			    !(message.Author is SocketGuildUser user) )
				return;

			try
			{
				SocketTextChannel logChannel;
				if( server.Config.LogDeletedMessages && (logChannel = server.Guild.GetTextChannel(server.Config.LogChannelId)) != null && !(
					    (this.Client.ClearedMessageIDs.ContainsKey(server.Id) && this.Client.ClearedMessageIDs[server.Id].Contains(message.Id)) ||
					    server.IgnoredChannels.Contains(channel.Id) ||
					    server.Roles.Where(r => r.Value.LoggingIgnored).Any(r => user.Roles.Any(role => role.Id == r.Value.RoleId))) )
				{
					StringBuilder attachment = new StringBuilder();
					if( message.Attachments != null && message.Attachments.Any() )
						foreach( Attachment a in message.Attachments )
							if( !string.IsNullOrWhiteSpace(a.Url) )
								attachment.AppendLine(a.Url);

					MessageDeleteAuditLogData auditData = null;
					RestAuditLogEntry auditEntry = null;
					if( server.Guild.CurrentUser.GuildPermissions.ViewAuditLog )
					{
						await Task.Delay(500);
						try
						{
							auditEntry = await server.Guild.GetAuditLogsAsync(10)?.Flatten()?.FirstOrDefault(e => e != null && e.Action == ActionType.MessageDeleted && (auditData = e.Data as MessageDeleteAuditLogData) != null && auditData.ChannelId == c.Id && (Utils.GetTimeFromId(e.Id) + TimeSpan.FromHours(1)) > DateTime.UtcNow);
							//One huge line because black magic from .NET Core?
						}
						catch(Exception) { }
					}

					bool byAntispam = this.Client.AntispamMessageIDs.Contains(message.Id);
					string title = "Message Deleted" + (byAntispam ? " by Antispam" : auditEntry != null ? (" by " + auditEntry.User.GetUsername()) : "");
					if( server.Config.LogChannelEmbeds )
					{
						Color color = byAntispam ? this.AntispamLightColor : new Color(server.Config.LogMessagesColor);
						await logChannel.SendMessageAsync("", embed:
							GetLogEmbed(color, user?.GetAvatarUrl(), title, "in #" + channel.Name,
								message.Author.GetUsername(), message.Author.Id.ToString(),
								message.Id,
								"Message", message.Content.Replace("@everyone", "@-everyone").Replace("@here", "@-here"),
								message.Attachments.Any() ? "Files" : "", attachment.ToString()));
					}
					else
					{
						await logChannel.SendMessageSafe(
							GetLogMessage(title, "#" + channel.Name,
								message.Author.GetUsername(), message.Author.Id.ToString(),
								message.Id,
								"Message", message.Content.Replace("@everyone", "@-everyone").Replace("@here", "@-here"),
								message.Attachments.Any() ? "Files" : "", attachment.ToString()));
					}
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "OnMessageDeleted", server.Id);
			}
		}

		private async Task OnMessageUpdated(SocketMessage originalMessage, SocketMessage updatedMessage, ISocketMessageChannel c)
		{
			if( originalMessage == null || updatedMessage == null ||
			    originalMessage.Content == updatedMessage.Content ||
			    !(c is SocketTextChannel channel) )
				return;

			Server server;
			if( !this.Client.Servers.ContainsKey(channel.Guild.Id) ||
			    (server = this.Client.Servers[channel.Guild.Id]) == null ||
			    server.Config.IgnoreBots && updatedMessage.Author.IsBot ||
			    !(updatedMessage.Author is SocketGuildUser user) )
				return;

			try
			{
				SocketTextChannel logChannel;
				if( server.Config.LogEditedMessages && (logChannel = server.Guild.GetTextChannel(server.Config.LogChannelId)) != null && !(
					    server.IgnoredChannels.Contains(channel.Id) ||
					    server.Roles.Where(r => r.Value.LoggingIgnored).Any(r => user.Roles.Any(role => role.Id == r.Value.RoleId))) )
				{

					if( server.Config.LogChannelEmbeds )
					{
						await logChannel.SendMessageAsync("", embed:
							GetLogEmbed(new Color(server.Config.LogMessagesColor), user?.GetAvatarUrl(),
								"Message Edited", "in #" + channel.Name,
								updatedMessage.Author.GetUsername(), updatedMessage.Author.Id.ToString(),
								updatedMessage.Id,
								"Before", originalMessage.Content.Replace("@everyone", "@-everyone").Replace("@here", "@-here"),
								"After", updatedMessage.Content.Replace("@everyone", "@-everyone").Replace("@here", "@-here")));
					}
					else
					{
						await logChannel.SendMessageSafe(
							GetLogMessage("Message Edited", "#" + channel.Name,
								updatedMessage.Author.GetUsername(), updatedMessage.Author.Id.ToString(),
								updatedMessage.Id,
								"Before", originalMessage.Content.Replace("@everyone", "@-everyone").Replace("@here", "@-here"),
								"After", updatedMessage.Content.Replace("@everyone", "@-everyone").Replace("@here", "@-here")));
					}
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "OnMessageUpdated", server.Id);
			}
		}


		private async Task OnUserBanned(SocketUser user, SocketGuild guild)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(guild.Id) || (server = this.Client.Servers[guild.Id]) == null ||
			    this.RecentlyBannedUserIDs.Contains(user.Id) )
				return;

			BanAuditLogData auditData = null;
			RestAuditLogEntry auditEntry = null;
			if( guild.CurrentUser.GuildPermissions.ViewAuditLog )
			{
				await Task.Delay(500);
				try
				{
					auditEntry = await guild.GetAuditLogsAsync(10)?.Flatten()?.FirstOrDefault(e => e != null && e.Action == ActionType.Ban && (auditData = e.Data as BanAuditLogData) != null && auditData.Target.Id == user.Id);
					//One huge line because black magic from .NET Core?
				}
				catch(Exception) { }
			}

			string reason = "unknown";
			RestBan ban = await server.Guild.GetBanAsync(user);
			if( ban != null )
			{
				reason = ban.Reason;
				await this.Client.Events.AddBan(guild.Id, user.Id, TimeSpan.Zero, reason);
			}
			await LogBan(server, user.GetUsername(), user.Id, reason, "permanently", auditEntry?.User as SocketGuildUser);
		}

		private async Task LogBan(Server server, string userName, guid userId, string reason, string duration, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogBans || (logChannel = server.Guild.GetTextChannel(server.Config.ModChannelId)) == null )
					return;

				this.RecentlyBannedUserIDs.Add(userId); //Don't trigger the on-event log message as well as this custom one.


				if( server.Config.ModChannelEmbeds )
				{
					Color color = issuedBy?.Id == this.Client.GlobalConfig.UserId ? this.AntispamColor : new Color(server.Config.ModChannelColor);
					await logChannel.SendMessageAsync("", embed:
						GetLogEmbed(color, "", "User Banned " + duration,
							"by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							userName ?? "<unknown>", userId.ToString(),
							DateTime.UtcNow,
							"Reason", reason));
				}
				else
				{
					await logChannel.SendMessageSafe(
						GetLogMessage("User Banned " + duration, (issuedBy == null ? "by unknown" : "by " + issuedBy.GetUsername()),
							userName ?? "", userId.ToString(),
							Utils.GetTimestamp(),
							"Reason", reason));
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogBan", server.Id);
			}
		}

		private async Task LogUnban(Server server, string userName, guid userId, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogBans || (logChannel = server.Guild.GetTextChannel(server.Config.ModChannelId)) == null )
					return;

				this.RecentlyUnbannedUserIDs.Add(userId); //Don't trigger the on-event log message as well as this custom one.

				if( server.Config.ModChannelEmbeds )
				{
					await logChannel.SendMessageAsync("", embed:
						GetLogEmbed(new Color(server.Config.ModChannelColor), "", "User Unbanned",
							"by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							userName ?? "<unknown>", userId.ToString(),
							DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe(
						GetLogMessage("User Unbanned", (issuedBy == null ? "by unknown" : "by " + issuedBy.GetUsername()),
							userName ?? "", userId.ToString(),
							Utils.GetTimestamp()));
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogUnban", server.Id);
			}
		}

		private async Task LogKick(Server server, string userName, guid userId, string reason, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogBans || (logChannel = server.Guild.GetTextChannel(server.Config.ModChannelId)) == null )
					return;

				this.RecentlyBannedUserIDs.Add(userId); //Don't trigger the on-event log message as well as this custom one.

				if( server.Config.ModChannelEmbeds )
				{
					Color color = issuedBy.Id == this.Client.GlobalConfig.UserId ? this.AntispamColor : new Color(server.Config.ModChannelColor);
					await logChannel.SendMessageAsync("", embed:
						GetLogEmbed(color, "", "User Kicked",
							"by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							userName ?? "<unknown>", userId.ToString(),
							DateTime.UtcNow,
							"Reason", reason));
				}
				else
				{
					await logChannel.SendMessageSafe(
						GetLogMessage("User Kicked", (issuedBy == null ? "by unknown" : "by " + issuedBy.GetUsername()),
							userName ?? "", userId.ToString(),
							Utils.GetTimestamp(),
							"Reason", reason));
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogKick", server.Id);
			}
		}

		private async Task LogMute(Server server, SocketGuildUser user, string duration, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogBans || (logChannel = server.Guild.GetTextChannel(server.Config.ModChannelId)) == null )
					return;

				if( server.Config.ModChannelEmbeds )
				{
					Color color = issuedBy.Id == this.Client.GlobalConfig.UserId ? this.AntispamColor : new Color(server.Config.ModChannelColor);
					await logChannel.SendMessageAsync("", embed:
						GetLogEmbed(color, user?.GetAvatarUrl(), "User muted " + duration,
							"by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							user.GetUsername(), user.Id.ToString(),
							DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe(
						GetLogMessage("User Muted " + duration, (issuedBy == null ? "by unknown" : "by " + issuedBy.GetUsername()),
							user.GetUsername(), user.Id.ToString(),
							Utils.GetTimestamp()));
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogMute", server.Id);
			}
		}

		private async Task LogUnmute(Server server, SocketGuildUser user, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogBans || (logChannel = server.Guild.GetTextChannel(server.Config.ModChannelId)) == null )
					return;

				if( server.Config.ModChannelEmbeds )
				{
					await logChannel.SendMessageAsync("", embed:
						GetLogEmbed(new Color(server.Config.ModChannelColor), user?.GetAvatarUrl(), "User Unmuted",
							"by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							user.GetUsername(), user.Id.ToString(),
							DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe(
						GetLogMessage("User Unmuted ", (issuedBy == null ? "by unknown" : "by " + issuedBy.GetUsername()),
							user.GetUsername(), user.Id.ToString(),
							Utils.GetTimestamp()));
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogUnmute", server.Id);
			}
		}
		private async Task LogMutedChannel(Server server, SocketGuildChannel channel, string duration, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogBans || (logChannel = server.Guild.GetTextChannel(server.Config.ModChannelId)) == null )
					return;

				if( server.Config.ModChannelEmbeds )
				{
					Color color = issuedBy.Id == this.Client.GlobalConfig.UserId ? this.AntispamColor : new Color(server.Config.ModChannelColor);
					await logChannel.SendMessageAsync("", embed:
						GetLogEmbed(color, "", "Channel muted " + duration,
							"by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							"#" + channel.Name, channel.Id.ToString(),
							DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe(
						GetLogMessage("Channel Muted " + duration, (issuedBy == null ? "by unknown" : "by " + issuedBy.GetUsername()),
							"#" + channel.Name, channel.Id.ToString(),
							Utils.GetTimestamp()));
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogMute", server.Id);
			}
		}

		private async Task LogUnmutedChannel(Server server, SocketGuildChannel channel, SocketUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogBans || (logChannel = server.Guild.GetTextChannel(server.Config.ModChannelId)) == null )
					return;

				if( server.Config.ModChannelEmbeds )
				{
					await logChannel.SendMessageAsync("", embed:
						GetLogEmbed(new Color(server.Config.ModChannelColor), "", "Channel Unmuted",
							"by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							"#" + channel.Name, channel.Id.ToString(),
							DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe(
						GetLogMessage("Channel Unmuted ", (issuedBy == null ? "by unknown" : "by " + issuedBy.GetUsername()),
							"#" + channel.Name, channel.Id.ToString(),
							Utils.GetTimestamp()));
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogUnmute", server.Id);
			}
		}


		private async Task LogPublicRoleJoin(Server server, SocketGuildUser user, string roleName)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogPromotions || (logChannel = server.Guild.GetTextChannel(server.Config.LogChannelId)) == null )
					return;

				if( server.Config.LogChannelEmbeds )
				{
					await logChannel.SendMessageAsync("", embed:
						GetLogSmolEmbed(new Color(server.Config.LogChannelColor),
							user.GetUsername() + " joined a publicRole:",
							user.GetAvatarUrl(), roleName,
							Utils.GetTimestamp(DateTime.UtcNow), DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe($"`{Utils.GetTimestamp()}`: **{user.GetUsername()}** joined the `{roleName}` public role.");
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogPublicRoleJoin", server.Id);
			}
		}

		private async Task LogPublicRoleLeave(Server server, SocketGuildUser user, string roleName)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogPromotions || (logChannel = server.Guild.GetTextChannel(server.Config.LogChannelId)) == null )
					return;

				if( server.Config.LogChannelEmbeds )
				{
					await logChannel.SendMessageAsync("", embed:
						GetLogSmolEmbed(new Color(server.Config.LogChannelColor),
							user.GetUsername() + " left a publicRole:",
							user.GetAvatarUrl(), roleName,
							Utils.GetTimestamp(DateTime.UtcNow), DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe($"`{Utils.GetTimestamp()}`: **{user.GetUsername()}** left the `{roleName}` public role.");
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogPublicRoleLeave", server.Id);
			}
		}

		private async Task LogPromote(Server server, SocketGuildUser user, string roleName, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogPromotions || (logChannel = server.Guild.GetTextChannel(server.Config.LogChannelId)) == null )
					return;

				if( server.Config.LogChannelEmbeds )
				{
					await logChannel.SendMessageAsync("", embed:
						GetLogSmolEmbed(new Color(server.Config.LogChannelColor),
							user.GetUsername() + $" was promoted to the {roleName} memberRole.",
							user.GetAvatarUrl(),
							"Promoted by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							Utils.GetTimestamp(DateTime.UtcNow), DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe($"`{Utils.GetTimestamp()}`: **{user.GetUsername()}** was promoted to the `{roleName}` member role by __{issuedBy.GetUsername()}__");
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogPromote", server.Id);
			}
		}

		private async Task LogDemote(Server server, SocketGuildUser user, string roleName, SocketGuildUser issuedBy)
		{
			try
			{
				SocketTextChannel logChannel;
				if( !server.Config.LogPromotions || (logChannel = server.Guild.GetTextChannel(server.Config.LogChannelId)) == null )
					return;

				if( server.Config.LogChannelEmbeds )
				{
					await logChannel.SendMessageAsync("", embed:
						GetLogSmolEmbed(new Color(server.Config.LogChannelColor),
							user.GetUsername() + $" was demoted from the {roleName} memberRole.",
							user.GetAvatarUrl(),
							"Demoted by: " + (issuedBy?.GetUsername() ?? "<unknown>"),
							Utils.GetTimestamp(DateTime.UtcNow), DateTime.UtcNow));
				}
				else
				{
					await logChannel.SendMessageSafe($"`{Utils.GetTimestamp()}`: **{user.GetUsername()}** was demoted from the `{roleName}` member role by __{issuedBy.GetUsername()}__");
				}
			}
			catch(HttpException) { }
			catch(Exception exception)
			{
				await this.HandleException(exception, "LogDemote", server.Id);
			}
		}


		public static string GetLogMessage(string titleRed, string infoGreen, string nameGold, string idGreen,
			guid timestampId, string tag1 = "", string msg1 = "", string tag2 = "", string msg2 = "")
			=> GetLogMessage(titleRed, infoGreen, nameGold, idGreen,
				Utils.GetTimestamp(Utils.GetTimeFromId(timestampId)),
				tag1, msg1, tag2, msg2);

		public static string GetLogMessage(string titleRed, string infoGreen, string nameGold, string idGreen,
			string timestamp, string tag1 = "", string msg1 = "", string tag2 = "", string msg2 = "")
		{
			msg1 = msg1.Replace('`', '\'');
			msg2 = msg2.Replace('`', '\'');
			int length = titleRed.Length + infoGreen.Length + nameGold.Length + idGreen.Length + msg1.Length + msg2.Length + timestamp.Length + 100;
			int messageLimit = 1500;
			while( length >= GlobalConfig.MessageCharacterLimit )
			{
				msg1 = msg1.Substring(0, Math.Min(messageLimit, msg1.Length)) + "**...**";
				if( !string.IsNullOrWhiteSpace(msg2) )
					msg2 = msg2.Substring(0, Math.Min(messageLimit, msg2.Length)) + "**...**";

				length = titleRed.Length + infoGreen.Length + nameGold.Length + idGreen.Length + msg1.Length + msg2.Length + timestamp.Length + 100;
				messageLimit -= 100;
			}

			string message = "";
			string tag = "";
			if( string.IsNullOrWhiteSpace(tag1) && !string.IsNullOrWhiteSpace(msg1) )
				message += msg1;
			else if( !string.IsNullOrWhiteSpace(tag1) && !string.IsNullOrWhiteSpace(msg1) )
			{
				tag = "<" + tag1;
				while( tag.Length < 9 )
					tag += " ";
				message += tag + "> " + msg1;
			}

			if( string.IsNullOrWhiteSpace(tag2) && !string.IsNullOrWhiteSpace(msg2) )
				message += "\n" + msg2;
			else if( !string.IsNullOrWhiteSpace(tag2) && !string.IsNullOrWhiteSpace(msg2) )
			{
				tag = "<" + tag2;
				while( tag.Length < 9 )
					tag += " ";
				message += "\n" + tag + "> " + msg2;
			}

			return string.Format("```md\n# {0}\n[{1}]({2})\n< {3} ={4}>\n{5}\n```", titleRed, timestamp, infoGreen, nameGold, idGreen, message);
		}


		public static Embed GetLogEmbed(Color color, string iconUrl, string title, string description, string name, string id,
			guid timestampId, string tag1 = "", string msg1 = "", string tag2 = "", string msg2 = "")
			=> GetLogEmbed(color, iconUrl, title, description, name, id,
				Utils.GetTimeFromId(timestampId),
				tag1, msg1, tag2, msg2);

		public static Embed GetLogEmbed(Color color, string iconUrl,string title, string description, string name, string id,
			DateTime timestamp, string tag1 = "", string msg1 = "", string tag2 = "", string msg2 = "")
		{
			if( msg1.Length > 1000 )
				msg1 = msg1.Substring(0, 1000) + "**...**";
			if( msg2.Length > 1000 )
				msg2 = msg2.Substring(0, 1000) + "**...**";

			msg1 = msg1.Replace('`', '\'');
			msg2 = msg2.Replace('`', '\'');

			EmbedBuilder embedBuilder = new EmbedBuilder{
					Color = color,
					Description = description,
					Timestamp = timestamp
				}.WithAuthor(title, iconUrl)
				 .WithFooter(Utils.GetTimestamp(timestamp));

			embedBuilder.AddField("Name", name, true);
			embedBuilder.AddField("Id", id, true);

			if( !string.IsNullOrEmpty(tag1) && !string.IsNullOrEmpty(msg1) )
				embedBuilder.AddField(tag1, msg1, false);
			if( !string.IsNullOrEmpty(tag2) && !string.IsNullOrEmpty(msg2) )
				embedBuilder.AddField(tag2, msg2, false);

			return embedBuilder.Build();
		}

		public static Embed GetLogSmolEmbed(Color color, string title, string titleIconUrl, string description, string footer, DateTime timestamp)
		{
			EmbedBuilder embedBuilder = new EmbedBuilder{
					Color = color,
					Description = description,
					Timestamp = timestamp
				}.WithAuthor(title, titleIconUrl)
				 .WithFooter(footer);

			return embedBuilder.Build();
		}


		public Task Update(IBotwinderClient iClient)
		{
			if( this.LastUpdateTime + this.UpdateDelay > DateTime.UtcNow )
				return Task.CompletedTask;

			this.LastUpdateTime = DateTime.UtcNow;

			this.RecentlyBannedUserIDs.Clear();
			this.RecentlyUnbannedUserIDs.Clear();

			return Task.CompletedTask;
		}
	}
}
