using System;
using System.Collections.Generic;
using System.Linq;
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
			//BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
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
					await iClient.SendMessageToChannel(e.Channel, string.Format("I'm really sorry, buuut `{0}op` feature is not configured! Poke your admin to set it up at <http://botwinder.info/config>", e.Server.ServerConfig.CommandCharacter));
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
					if( ex != null )
					{
						if( ex.HttpCode == System.Net.HttpStatusCode.Forbidden )
							response = "Something went wrong, I may not have server permissions to do that.\n(Hint: Botwinder has to be above other roles to be able to manage them: <http://i.imgur.com/T8MPvME.png>)";
					}
					else
					{
						await this.HandleException(exception, "op command", e.Server.Id);
						response = "An unknown error has occurred.";
					}
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
