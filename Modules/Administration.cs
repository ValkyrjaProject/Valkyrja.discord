using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Administration: IModule
	{
		private BotwinderClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !removeStreamPermission
			Command newCommand = new Command("removeStreamPermission");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Removes Stream permission from all the roles.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe("I ain't got the permissions.");
					return;
				}

				int highestRolePosition = 0;
				foreach( SocketRole role in e.Server.Guild.Roles )
				{
					if( role.Position > highestRolePosition )
					highestRolePosition = role.Position;
				}

				if( e.Server.Guild.CurrentUser.Hierarchy < highestRolePosition )
				{
					await e.SendReplySafe("I am not the top role in hierarchy. I really have to be on top to disable that thing on all the roles!");
					return;
				}

				int exceptions = 0;
				string response = "Done with exceptions:\n";
				foreach( SocketRole role in e.Server.Guild.Roles.Where(r => r.Position < e.Server.Guild.CurrentUser.Hierarchy) )
				{
					try
					{
						await role.ModifyAsync(r => r.Permissions = new Optional<GuildPermissions>(role.Permissions.Modify(stream: false)));
					}
					catch(Exception)
					{
						response += $"`{role.Name}`\n";
						if( ++exceptions > 5 )
						{
							response += "...and probably more...";
							break;
						}
					}
				}

				if( exceptions == 0 )
					response = "Done.";

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removeGolivePermission"));

			return commands;
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
