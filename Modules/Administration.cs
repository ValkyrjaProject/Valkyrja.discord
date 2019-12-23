using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Valkyrja.modules
{
	public class Administration: IModule
	{
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		private Regex EmojiNameRegex = new Regex("\\w+", RegexOptions.Compiled);

		private ValkyrjaClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

// !getRole
			Command newCommand = new Command("getRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Get a name, id and color of `roleID` or `roleName` parameter.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				guid id = 0;
				SocketRole roleFromId = null;
				List<SocketRole> roles = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) ||
				    (!(guid.TryParse(e.TrimmedMessage, out id) && (roleFromId = e.Server.Guild.GetRole(id)) != null) &&
				     !(roles = e.Server.Guild.Roles.Where(r => r.Name.ToLower().Contains(e.TrimmedMessage.ToLower())).ToList()).Any()) )
				{
					await e.SendReplySafe("Role not found.");
					return;
				}

				if( roleFromId != null )
				{
					roles = new List<SocketRole>();
					roles.Add(roleFromId);
				}

				StringBuilder response = new StringBuilder();
				foreach(SocketRole role in roles)
				{
					string hex = BitConverter.ToString(new byte[]{role.Color.R, role.Color.G, role.Color.B}).Replace("-", "");
					response.AppendLine($"Role: `{role.Name}`\n  Id: `{role.Id}`\n  Position: `{role.Position}`\n  Color: `rgb({role.Color.R},{role.Color.G},{role.Color.B})` | `hex(#{hex})`");
				}

				await e.SendReplySafe(response.ToString());
			};
			commands.Add(newCommand);

// !addEmojiRole
			newCommand = new Command("addEmojiRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Add an emoji reaction assigned role to a message.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( e.MessageArgs == null || e.MessageArgs.Length < 3 || e.TrimmedMessage == "-h" || e.TrimmedMessage == "--help" || !guid.TryParse(e.MessageArgs[0], out guid messageId) )
				{
					await e.SendReplySafe($"Usage: `{e.Server.Config.CommandPrefix}{e.CommandId} <messageId> <emoji> <roleId or name expression>`");
					return;
				}

				Match emoji = this.EmojiNameRegex.Match(e.MessageArgs[1]);
				if( !emoji.Success )
				{
					await e.SendReplySafe("What emoji again?");
					return;
				}

				SocketRole role = e.Server.GetRole(e.MessageArgs[2], out string response);
				if( role == null )
				{
					await e.SendReplySafe(response);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				ReactionAssignedRole reactionRole = dbContext.ReactionAssignedRoles.FirstOrDefault(r => r.ServerId == e.Server.Id && r.MessageId == messageId && r.Emoji == emoji.Value);
				if( reactionRole == null )
					dbContext.ReactionAssignedRoles.Add(reactionRole = new ReactionAssignedRole(){
						ServerId = e.Server.Id,
						MessageId = messageId,
						Emoji = emoji.Value
					});
				else if( reactionRole.RoleId == role.Id )
				{
					await e.SendReplySafe("Already exists.");
					dbContext.Dispose();
					return;
				}

				reactionRole.RoleId = role.Id;
				dbContext.SaveChanges();
				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !membersOf
			newCommand = new Command("membersOf");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display a list of members of a role.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				SocketRole role = e.Server.GetRole(e.TrimmedMessage, out string response);
				if( role == null )
				{
					await e.SendReplySafe(response);
					return;
				}

				await e.Server.Guild.DownloadUsersAsync();
				List<string> names = role.Members.Select(u => u.GetUsername()).ToList();
				names.Sort();

				response = names.Count == 0 ? "Nobody has this role." : $"Members of `{role.Name}` are:\n" + names.ToNamesList();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("listMembers"));

// !createRole
			newCommand = new Command("createRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Create a role with specified name.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.Message.Channel.SendMessageSafe(ErrorPermissionsString);
					return;
				}
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe("What role? Name? Do you want me to come up with something silly or what?");
					return;
				}

				RestRole role = await e.Server.Guild.CreateRoleAsync(e.TrimmedMessage, GuildPermissions.None);
				string response = $"Role created: `{role.Name}`\n  Id: `{role.Id}`";
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !createRoles
			newCommand = new Command("createRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Create roles with specified names. List of whitespace delimited arguments, use quotes to use spaces.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.Message.Channel.SendMessageSafe(ErrorPermissionsString);
					return;
				}
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(e.Command.Description);
					return;
				}

				StringBuilder response = new StringBuilder("Roles created:\n");

				for( int i = 0; i < e.MessageArgs.Length; i++ )
				{
					RestRole role = await e.Server.Guild.CreateRoleAsync(e.MessageArgs[i], GuildPermissions.None);
					response.AppendLine($"`{role.Id}` | `{role.Name}`");
				}

				await e.SendReplySafe(response.ToString());
			};
			commands.Add(newCommand);

// !createPublicRoles
			newCommand = new Command("createPublicRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Create public roles with specified names. The first argument will be used as a name for the new role group, followed by a list of whitespace delimited arguments, use quotes to use spaces.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.Message.Channel.SendMessageSafe(ErrorPermissionsString);
					return;
				}
				if( e.MessageArgs.Length < 2 )
				{
					await e.SendReplySafe(e.Command.Description);
					return;
				}

				StringBuilder response = new StringBuilder("Roles created:\n");

				Int64 groupId = 1;
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				IEnumerable<RoleGroupConfig> roleGroups = dbContext.PublicRoleGroups.Where(g => g.ServerId == e.Server.Id);
				foreach( RoleGroupConfig group in roleGroups )
				{
					if( groupId < group.GroupId )
						groupId = group.GroupId;
				}

				RoleGroupConfig roleGroup = new RoleGroupConfig(){
					ServerId = e.Server.Id,
					GroupId = groupId,
					Name = e.MessageArgs[0],
					RoleLimit = 1
				};
				dbContext.PublicRoleGroups.Add(roleGroup);

				bool save = true;
				for( int i = 1; i < e.MessageArgs.Length; i++ )
				{
					try
					{
						RestRole role = await e.Server.Guild.CreateRoleAsync(e.MessageArgs[i], GuildPermissions.None);

						RoleConfig roleConfig = new RoleConfig(){
							ServerId = e.Server.Id,
							RoleId = role.Id,
							PermissionLevel = RolePermissionLevel.Public,
							PublicRoleGroupId = groupId
						};
						dbContext.Roles.Add(roleConfig);

						response.AppendLine($"`{role.Id}` | `{role.Name}`");
					}
					catch(Exception)
					{
						save = false;
						response.AppendLine($"__Something went wrong__ :/");
						break;
					}
				}

				if( save )
				{
					dbContext.SaveChanges();
				}
				dbContext.Dispose();

				await e.SendReplySafe(response.ToString());
			};
			commands.Add(newCommand);

// !createTempRole
			newCommand = new Command("createTempRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "`createTempRole name time` Create a role with specified name, which will be destroyed after specified time (e.g. `7d` or `12h` or `1d12h`)";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				RoleConfig roleConfig = await CreateTempRole(e, dbContext);
				if( roleConfig != null )
					dbContext.SaveChanges();
				dbContext.Dispose();
			};
			commands.Add(newCommand);

// !createTempPublicRole
			newCommand = new Command("createTempPublicRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "`createTempRole name time` Create a role with specified name, which will be destroyed after specified time (e.g. `7d` or `12h` or `1d12h`) This role will also become a public - use the `join` command to get it.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				RoleConfig roleConfig = await CreateTempRole(e, dbContext);
				if( roleConfig != null )
				{
					roleConfig.PermissionLevel = RolePermissionLevel.Public;
					dbContext.SaveChanges();
				}
				dbContext.Dispose();
			};
			commands.Add(newCommand);

// !createColourRoles
			newCommand = new Command("createColourRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Create 9 roles with various colours, you can find emoji representations of these colours in Valhalla - the Valkyrja support server.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				await e.Server.Guild.CreateRoleAsync("purple", GuildPermissions.None, new Color(180,136,209));
				await e.Server.Guild.CreateRoleAsync("pink", GuildPermissions.None, new Color(255,183,255));
				await e.Server.Guild.CreateRoleAsync("orange", GuildPermissions.None, new Color(255,165,105));
				await e.Server.Guild.CreateRoleAsync("lightOrange", GuildPermissions.None, new Color(255,186,158));
				await e.Server.Guild.CreateRoleAsync("lightYellow", GuildPermissions.None, new Color(223,223,133));
				await e.Server.Guild.CreateRoleAsync("yellow", GuildPermissions.None, new Color(201,192,67));
				await e.Server.Guild.CreateRoleAsync("blue", GuildPermissions.None, new Color(92,221,255));
				await e.Server.Guild.CreateRoleAsync("cyan", GuildPermissions.None, new Color(150,232,221));
				await e.Server.Guild.CreateRoleAsync("green", GuildPermissions.None, new Color(46,204,113));

				await e.SendReplySafe("I've created them roles, but you're gonna have to set them up yourself at <https://valkyrja.app/config> because I don't know the details!\n" +
				                      $"_You can use my colour emojis to set them up as reaction assigned roles. Get them in Valhalla: {GlobalConfig.DiscordInvite}_");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("createColorRoles"));

// !removeStreamPermission
			newCommand = new Command("removeStreamPermission");
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

		public Task Update(IValkyrjaClient iClient)
		{
			return Task.CompletedTask;
		}

		private async Task<RoleConfig> CreateTempRole(CommandArguments e, ServerContext dbContext)
		{
			if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
			{
				await e.Message.Channel.SendMessageSafe(ErrorPermissionsString);
				return null;
			}
			if( string.IsNullOrEmpty(e.TrimmedMessage) )
			{
				await e.SendReplySafe("What role? Name? Do you want me to come up with something silly or what? And when do you want to nuke it?");
				return null;
			}

			if( e.MessageArgs.Length != 2 )
			{
				await e.SendReplySafe(e.Command.Description);
				return null;
			}

			int durationHours = 0;
			try
			{
				Match dayMatch = Regex.Match(e.MessageArgs[1], "\\d+d", RegexOptions.IgnoreCase);
				Match hourMatch = Regex.Match(e.MessageArgs[1], "\\d+h", RegexOptions.IgnoreCase);

				if( !hourMatch.Success && !dayMatch.Success )
				{
					await e.SendReplySafe(e.Command.Description);
					dbContext.Dispose();
					return null;
				}

				if( hourMatch.Success )
					durationHours = int.Parse(hourMatch.Value.Trim('h').Trim('H'));
				if( dayMatch.Success )
					durationHours += 24 * int.Parse(dayMatch.Value.Trim('d').Trim('D'));
			}
			catch(Exception)
			{
				await e.SendReplySafe(e.Command.Description);
				dbContext.Dispose();
				return null;
			}

			RoleConfig roleConfig = null;
			string response = Localisation.SystemStrings.DiscordShitEmoji;
			try
			{
				RestRole role = await e.Server.Guild.CreateRoleAsync(e.MessageArgs[0], GuildPermissions.None);
				roleConfig = dbContext.GetOrAddRole(e.Server.Id, role.Id);
				roleConfig.DeleteAtTime = DateTime.UtcNow + TimeSpan.FromHours(durationHours);
				response = $"Role created: `{role.Name}`\n  Id: `{role.Id}`\n  Delete at `{Utils.GetTimestamp(roleConfig.DeleteAtTime)}`";
			}
			catch(Exception exception)
			{
				await this.HandleException(exception, "CreateTempRole failed.", e.Server.Id);
			}

			await e.SendReplySafe(response);
			return roleConfig;
		}
	}
}
