using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Diagnostics;
using guid = System.UInt64;

namespace Botwinder.modules
{
	public class RoleAssignment: IModule
	{
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			BotwinderClient client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !publicRoles
			Command newCommand = new Command("publicRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "See what Public Roles can you join on this server.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, "I'm sorry, but there are no public roles on this server.");
					return;
				}

				StringBuilder responseBuilder = new StringBuilder(string.Format("You can use `{0}join` and `{0}leave` commands with these Public Roles: ", e.Server.Config.CommandPrefix));
				Dictionary<Int64, List<RoleConfig>> groups = new Dictionary<Int64, List<RoleConfig>>();

				foreach( RoleConfig roleConfig in publicRoles )
				{
					SocketRole role = e.Server.Guild.GetRole(roleConfig.RoleId);
					if( role == null )
						continue;

					if( !groups.ContainsKey(roleConfig.PublicRoleGroupId) )
					{
						List<RoleConfig> tempGroup = publicRoles.Where(r => r.PublicRoleGroupId == roleConfig.PublicRoleGroupId).ToList();
						groups.Add(roleConfig.PublicRoleGroupId, tempGroup);
					}
				}

				string GetRoleNames(List<RoleConfig> roleConfigs)
				{
					return e.Server.Guild.Roles.Where(r => roleConfigs.Any(rc => rc.RoleId == r.Id))
						.Select(r => r.Name).ToNames();
				}

				if( groups.ContainsKey(0) )
				{
					responseBuilder.Append(GetRoleNames(groups[0]));
				}

				bool hadGroup = false;
				foreach( KeyValuePair<Int64, List<RoleConfig>> group in groups )
				{
					if( group.Key == 0 )
						continue;

					hadGroup = true;
					responseBuilder.Append($"\n\n**Group #{group.Key}:** ");

					responseBuilder.Append(group.Value);
				}

				if( hadGroup )
				{
					responseBuilder.AppendLine("\n\n_(Where `Group` roles are exclusive - joining a `Group` role will remove any other role out of that group, that you already have.)_");
				}

				await iClient.SendMessageToChannel(e.Channel, responseBuilder.ToString());
			};
			commands.Add(newCommand);


			return commands;
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
