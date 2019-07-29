using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Botwinder.modules
{
	public class ExtraFeatures: IModule
	{
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		private const string ErrorRoleNotFound = "I did not find a role based on that expression.";
		private const string ErrorTooManyFound = "I found more than one role with that expression, please be more specific.";
		private const string ErrorUnknownString = "Unknown error, please poke <@{0}> to take a look x_x";
		private const string TempChannelConfirmString = "Here you go! <3\n_(Temporary channel `{0}` was created.)_";

		private BotwinderClient Client;

		private readonly TimeSpan TempChannelDelay = TimeSpan.FromMinutes(3);


		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !tempChannel
			Command newCommand = new Command("tempChannel");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Creates a temporary voice channel. This channel will be destroyed when it becomes empty, with grace period of three minutes since it's creation.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( e.Server.Config.TempChannelCategoryId == 0 )
				{
					await e.SendReplySafe("This command has to be configured on the config page (social) <https://valkyrja.app/config>");
					return;
				}

				if( string.IsNullOrWhiteSpace(e.TrimmedMessage) )
				{
					await e.SendReplySafe($"Usage: `{e.Server.Config.CommandPrefix}tempChannel <name>` or `{e.Server.Config.CommandPrefix}tempChannel [userLimit] <name>`");
					return;
				}

				int limit = 0;
				bool limited = int.TryParse(e.MessageArgs[0], out limit);
				StringBuilder name = new StringBuilder();
				for(int i = limited ? 1 : 0; i < e.MessageArgs.Length; i++)
				{
					name.Append(e.MessageArgs[i]);
					name.Append(" ");
				}
				string responseString = string.Format(TempChannelConfirmString, name.ToString());

				try
				{
					RestVoiceChannel tempChannel = null;
					if( limited )
						tempChannel = await e.Server.Guild.CreateVoiceChannelAsync(name.ToString(), c => {
							c.CategoryId = e.Server.Config.TempChannelCategoryId;
							c.UserLimit = limit;
						});
					else
						tempChannel = await e.Server.Guild.CreateVoiceChannelAsync(name.ToString(), c => c.CategoryId = e.Server.Config.TempChannelCategoryId);

					ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
					ChannelConfig channel = dbContext.Channels.FirstOrDefault(c => c.ServerId == e.Server.Id && c.ChannelId == tempChannel.Id);
					if( channel == null )
					{
						channel = new ChannelConfig{
							ServerId = e.Server.Id,
							ChannelId = tempChannel.Id
						};

						dbContext.Channels.Add(channel);
					}

					channel.Temporary = true;
					dbContext.SaveChanges();
					dbContext.Dispose();
				}
				catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					responseString = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}
				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("tmp"));
			commands.Add(newCommand.CreateAlias("tc"));

// !mentionRole
			newCommand = new Command("mentionRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Mention a role with a message. Use with the name of the role as the first parameter and the message will be the rest.";
			newCommand.DeleteRequest = true;
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					await e.SendReplyUnsafe($"Usage: `{e.Server.Config.CommandPrefix}{e.CommandId} <roleName> <message text>`");
					return;
				}

				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = e.Server.Guild.Roles.Where(r => r.Name == e.MessageArgs[0])).Any() &&
				    !(foundRoles = e.Server.Guild.Roles.Where(r => r.Name.ToLower() == e.MessageArgs[0].ToLower())).Any() &&
				    !(foundRoles = e.Server.Guild.Roles.Where(r => r.Name.ToLower().Contains(e.MessageArgs[0].ToLower()))).Any() )
				{
					await e.SendReplyUnsafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplyUnsafe(ErrorTooManyFound);
					return;
				}

				SocketRole role = foundRoles.First();
				string message = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[1]));

				await role.ModifyAsync(r => r.Mentionable = true);
				await Task.Delay(100);
				await e.SendReplySafe($"{role.Mention} {message}");
				await Task.Delay(100);
				await role.ModifyAsync(r => r.Mentionable = false);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("announce"));

			return commands;
		}

		public async Task Update(IBotwinderClient iClient)
		{
			BotwinderClient client = iClient as BotwinderClient;
			ServerContext dbContext = ServerContext.Create(client.DbConnectionString);
			bool save = false;
			DateTime minTime = DateTime.MinValue + TimeSpan.FromMinutes(1);

			//Channels
			List<ChannelConfig> channelsToRemove = new List<ChannelConfig>();
			foreach( ChannelConfig channelConfig in dbContext.Channels.Where(c => c.Temporary ) )
			{
				Server server;
				if( !client.Servers.ContainsKey(channelConfig.ServerId) ||
				    (server = client.Servers[channelConfig.ServerId]) == null )
					continue;

				//Temporary voice channels
				SocketGuildChannel channel = server.Guild.GetChannel(channelConfig.ChannelId);
				if( channel != null &&
				    channel.CreatedAt < DateTimeOffset.UtcNow - this.TempChannelDelay &&
				    !channel.Users.Any() )
				{
					try
					{
						await channel.DeleteAsync();
						channelConfig.Temporary = false;
						channelsToRemove.Add(channelConfig);
						save = true;
					}
					catch(Exception) { }
				}
				else if( channel == null )
				{
					channelConfig.Temporary = false;
					channelsToRemove.Add(channelConfig);
					save = true;
				}
			}

			if( channelsToRemove.Any() )
				dbContext.Channels.RemoveRange(channelsToRemove);

			if( save )
				dbContext.SaveChanges();
			dbContext.Dispose();
		}
	}
}
