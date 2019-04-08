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
	public class RoleAssignment: IModule
	{
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		private static string ErrorNoPublicRoles = "I'm sorry, but there are no public roles on this server.";
		private static string ErrorNoMemberRoles = "I'm sorry, but there are no member roles on this server.";
		private static string ErrorTooManyFound = "I found more than one role with that expression, please be more specific.";
		private static string ErrorTooManyGroupsFound = "I found more than one group with that expression, please be more specific.";
		private static string ErrorRoleNotFound = "I did not find a role based on that expression.";
		private static string ErrorGroupNotFound = "I did not find a group based on that expression.";
		private static string ErrorRoleNotFoundId = "Role not found. Use with roleID parameter.\n(Use the `getRole` command to get the ID)";
		private static string ErrorPromoteEveryone = "Failed to assign the role to more than 10 people - aborting. (Assuming wrong permissions or hierarchy.)";
		private static string ErrorKickWithoutRole = "Failed to kick more than 10 people - aborting. (Assuming wrong permissions, hierarchy, or Discord derps much.)";
		private static string PromoteEveryoneResponseString = "I will assign a role to everyone, which may take **very** long time. Please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";
		private static string DemoteEveryoneResponseString = "I will remove a role from everyone, which may take **very** long time. Please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";
		private static string WorkingOnItString = "Working on it. This may take some time so please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";
		private static string FoundCountString = "There are {0} members without any role.";

		private BotwinderClient Client;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.UserJoined += OnUserJoined;
			this.Client.Events.ReactionAdded += OnReactionAdded;
			this.Client.Events.ReactionRemoved += OnReactionRemoved;

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
			commands.Add(newCommand.CreateAlias("getrole"));

// !membersOf
			newCommand = new Command("membersOf");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display a list of members of a role.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				string expression = e.TrimmedMessage;

				guid id = 0;
				IEnumerable<SocketRole> roles = e.Server.Guild.Roles;
				IEnumerable<SocketRole> foundRoles = null;
				SocketRole role = null;

				if( !(guid.TryParse(expression, out id) && (role = e.Server.Guild.GetRole(id)) != null) &&
					!(foundRoles = roles.Where(r => r.Name == expression)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == expression.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(expression.ToLower()))).Any() )
				{
					await e.SendReplySafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplySafe(ErrorTooManyFound);
					return;
				}

				if( role == null )
				{
					role = foundRoles.First();
				}

				await e.Server.Guild.DownloadUsersAsync();
				List<string> names = role.Members.Select(u => u.GetUsername()).ToList();
				names.Sort();

				string response = names.Count == 0 ? "Nobody has this role." : $"Members of `{role.Name}` are:\n" + names.ToNamesList();
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
				                      "_You can use my colour emojis to set them up as reaction assigned roles. Get them in Valhalla: https://discord.gg/XgVvkXx_");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("createColorRoles"));

// !publicRoles
			newCommand = new Command("publicRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "See what Public Roles can you join on this server.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoPublicRoles);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				StringBuilder responseBuilder = new StringBuilder(string.Format("You can use `{0}join` and `{0}leave` commands with these Public Roles: ", e.Server.Config.CommandPrefix));
				Dictionary<Int64, List<RoleConfig>> groupRoles = new Dictionary<Int64, List<RoleConfig>>();
				Dictionary<Int64, RoleGroupConfig> groupConfigs = dbContext.PublicRoleGroups.Where(g => g.ServerId == e.Server.Id).ToDictionary(g => g.GroupId);
				dbContext.Dispose();

				foreach( RoleConfig roleConfig in publicRoles )
				{
					SocketRole role = e.Server.Guild.GetRole(roleConfig.RoleId);
					if( role == null )
						continue;

					if( !groupRoles.ContainsKey(roleConfig.PublicRoleGroupId) )
					{
						List<RoleConfig> tempGroup = publicRoles.Where(r => r.PublicRoleGroupId == roleConfig.PublicRoleGroupId).ToList();
						groupRoles.Add(roleConfig.PublicRoleGroupId, tempGroup);
					}
				}

				string GetRoleNames(List<RoleConfig> roleConfigs)
				{
					return e.Server.Guild.Roles.Where(r => roleConfigs.Any(rc => rc.RoleId == r.Id))
						.Select(r => r.Name).ToNames();
				}

				if( groupRoles.ContainsKey(0) )
				{
					responseBuilder.Append(GetRoleNames(groupRoles[0]));
				}

				foreach( KeyValuePair<Int64, List<RoleConfig>> groupRole in groupRoles )
				{
					if( groupRole.Key == 0 )
						continue;

					RoleGroupConfig groupConfig = groupConfigs.ContainsKey(groupRole.Key) ? groupConfigs[groupRole.Key] : new RoleGroupConfig();
					string name = string.IsNullOrEmpty(groupConfig.Name) ? ("Group #" + groupRole.Key.ToString()) : groupConfig.Name;
					responseBuilder.Append($"\n\n**{name}** - you can join {groupConfig.RoleLimit} of these:\n");

					responseBuilder.Append(GetRoleNames(groupRole.Value));
				}

				await e.SendReplySafe(responseBuilder.ToString());
			};
			commands.Add(newCommand);

// !roleCounts
			newCommand = new Command("roleCounts");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Get some numbers about public roles for specific group.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				string expression = e.TrimmedMessage;

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				IEnumerable<RoleGroupConfig> roleGroups = dbContext.PublicRoleGroups.Where(g => g.ServerId == e.Server.Id);
				IEnumerable<RoleGroupConfig> foundGroups = null;

				if( string.IsNullOrEmpty(expression) || (
					!(foundGroups = roleGroups.Where(g => g.Name == expression)).Any() &&
				    !(foundGroups = roleGroups.Where(g => (g.Name?.ToLower() ?? "") == expression.ToLower())).Any() &&
				    !(foundGroups = roleGroups.Where(g => g.Name?.ToLower().Contains(expression.ToLower()) ?? false )).Any()) )
				{
					await e.SendReplySafe(ErrorGroupNotFound);
					dbContext.Dispose();
					return;
				}

				if( foundGroups.Count() > 1 )
				{
					await e.SendReplySafe(ErrorTooManyGroupsFound);
					dbContext.Dispose();
					return;
				}

				await e.Server.Guild.DownloadUsersAsync();

				Int64 groupId = foundGroups.First().GroupId;
				dbContext.Dispose();

				IEnumerable<guid> roleIds = e.Server.Roles.Values.Where(r => r.PublicRoleGroupId == groupId).Select(r => r.RoleId);
				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => roleIds.Contains(r.Id));
				StringBuilder response = new StringBuilder();
				foreach( SocketRole role in roles )
				{
					response.AppendLine($"**{role.Name}**: `{role.Members.Count()}`");
				}

				await e.SendReplySafe(response.ToString());
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("countGroup"));
			commands.Add(newCommand.CreateAlias("countRoles"));

// !join
			newCommand = new Command("join");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameter, name of a Role that you wish to join.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplyUnsafe(e.Command.Description);
					return;
				}

				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoPublicRoles);
					return;
				}

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => publicRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == e.TrimmedMessage)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == e.TrimmedMessage.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(e.TrimmedMessage.ToLower()))).Any() )
				{
					await e.SendReplyUnsafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplyUnsafe(ErrorTooManyFound);
					return;
				}

				RoleGroupConfig groupConfig = null;
				IEnumerable<guid> groupRoleIds = null;
				SocketRole roleToAssign = foundRoles.First();
				Int64 groupId = publicRoles.First(r => r.RoleId == roleToAssign.Id).PublicRoleGroupId;

				if( groupId != 0 )
				{
					groupRoleIds = publicRoles.Where(r => r.PublicRoleGroupId == groupId).Select(r => r.RoleId);
					int userHasCount = (e.Message.Author as SocketGuildUser).Roles.Count(r => groupRoleIds.Any(id => id == r.Id));

					if( userHasCount > 0 )
					{
						ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
						groupConfig = dbContext.PublicRoleGroups.FirstOrDefault(g => g.ServerId == e.Server.Id && g.GroupId == groupId);
						dbContext.Dispose();

						if( groupConfig != null && groupConfig.RoleLimit > 1 && userHasCount >= groupConfig.RoleLimit )
						{
							await e.SendReplyUnsafe($"You can only have {groupConfig.RoleLimit} roles from the `{groupConfig.Name}` group.");
							return;
						}
					}
				}

				bool removed = false;
				string response = "Done!";
				try
				{
					SocketGuildUser user = (e.Message.Author as SocketGuildUser);

					if( groupRoleIds != null && (groupConfig == null || groupConfig.RoleLimit == 1) )
					{
						foreach( guid id in groupRoleIds )
						{
							if( user.Roles.All(r => r.Id != id) )
								continue;

							SocketRole roleToLeave = e.Server.Guild.GetRole(id);
							if( roleToLeave == null )
								continue;

							await user.RemoveRoleAsync(roleToLeave);
							removed = true;
						}
					}

					await user.AddRoleAsync(roleToAssign);
				} catch(Exception exception)
				{
					if( exception is Discord.Net.HttpException ex && (ex.HttpCode == System.Net.HttpStatusCode.Forbidden || (ex.DiscordCode.HasValue && ex.DiscordCode.Value == 50013) || exception.Message.Contains("Missing Access") || exception.Message.Contains("Missing Permissions")) )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				if( removed )
					response += "\n_(I've removed the other exclusive roles from the same role group.)_";

				await e.SendReplyUnsafe(response);

				if( this.Client.Events.LogPublicRoleJoin != null )
					await this.Client.Events.LogPublicRoleJoin(e.Server, e.Message.Author as SocketGuildUser, roleToAssign.Name);
			};
			commands.Add(newCommand);

// !leave
			newCommand = new Command("leave");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameter, name of a Role that you wish to leave.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplyUnsafe(e.Command.Description);
					return;
				}

				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoPublicRoles);
					return;
				}

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => publicRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == e.TrimmedMessage)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == e.TrimmedMessage.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(e.TrimmedMessage.ToLower()))).Any() )
				{
					await e.SendReplyUnsafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplyUnsafe(ErrorTooManyFound);
					return;
				}

				string response = "Done!";
				try
				{
					await (e.Message.Author as SocketGuildUser).RemoveRoleAsync(foundRoles.First());
				} catch(Exception exception)
				{
					if( exception is Discord.Net.HttpException ex && (ex.HttpCode == System.Net.HttpStatusCode.Forbidden || (ex.DiscordCode.HasValue && ex.DiscordCode.Value == 50013) || exception.Message.Contains("Missing Access") || exception.Message.Contains("Missing Permissions")) )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				await e.SendReplyUnsafe(response);

				if( this.Client.Events.LogPublicRoleLeave != null )
					await this.Client.Events.LogPublicRoleLeave(e.Server, e.Message.Author as SocketGuildUser, foundRoles.First().Name);
			};
			commands.Add(newCommand);

// !memberRoles
			newCommand = new Command("memberRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "See what Member Roles you can assign to others.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				List<RoleConfig> memberRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).ToList();
				if( memberRoles == null || memberRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoMemberRoles);
					return;
				}

				string response = string.Format("You can use `{0}promote` and `{0}demote` commands with these Member Roles: {1}",
					e.Server.Config.CommandPrefix,
					e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc.RoleId == r.Id)).Select(r => r.Name).ToNames()
				);

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !promote
			newCommand = new Command("promote");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Assign someone a member role. Use with parameters `@user` mention(s) or ID(s) and then the name of the role.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				List<SocketGuildUser> users;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !(users = this.Client.GetMentionedGuildUsers(e)).Any() || e.MessageArgs.Length <= users.Count )
				{
					await e.SendReplySafe(e.Command.Description);
					return;
				}

				List<RoleConfig> memberRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).ToList();
				if( memberRoles == null || memberRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoMemberRoles);
					return;
				}

				string expression = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[users.Count]));

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == expression)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == expression.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(expression.ToLower()))).Any() )
				{
					await e.SendReplySafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplySafe(ErrorTooManyFound);
					return;
				}

				string response = "Done!";
				SocketRole role = foundRoles.First();
				try
				{
					foreach( SocketGuildUser user in users )
					{
						await user.AddRoleAsync(role);

						if( this.Client.Events.LogPromote != null )
							await this.Client.Events.LogPromote(e.Server, user, role.Name, e.Message.Author as SocketGuildUser);
					}
				} catch(Exception exception)
				{
					if( exception is Discord.Net.HttpException ex && (ex.HttpCode == System.Net.HttpStatusCode.Forbidden || (ex.DiscordCode.HasValue && ex.DiscordCode.Value == 50013) || exception.Message.Contains("Missing Access") || exception.Message.Contains("Missing Permissions")) )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !demote
			newCommand = new Command("demote");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Remove a member role from someone. Use with parameters `@user` mention(s) or ID(s) and then the name of the role.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				List<SocketGuildUser> users;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !(users = this.Client.GetMentionedGuildUsers(e)).Any() || e.MessageArgs.Length <= users.Count)
				{
					await e.SendReplySafe(e.Command.Description);
					return;
				}

				List<RoleConfig> memberRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).ToList();
				if( memberRoles == null || memberRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoMemberRoles);
					return;
				}

				string expression = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[users.Count]));

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == expression)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == expression.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(expression.ToLower()))).Any() )
				{
					await e.SendReplySafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplySafe(ErrorTooManyFound);
					return;
				}

				SocketRole role = foundRoles.First();
				string response = "Done!";
				try
				{
					foreach( SocketGuildUser user in users )
					{
						await user.RemoveRoleAsync(role);

						if( this.Client.Events.LogDemote != null )
							await this.Client.Events.LogDemote(e.Server, user, role.Name, e.Message.Author as SocketGuildUser);
					}
				} catch(Exception exception)
				{
					if( exception is Discord.Net.HttpException ex && (ex.HttpCode == System.Net.HttpStatusCode.Forbidden || (ex.DiscordCode.HasValue && ex.DiscordCode.Value == 50013) || exception.Message.Contains("Missing Access") || exception.Message.Contains("Missing Permissions")) )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !promoteEveryone
			newCommand = new Command("promoteEveryone");
			newCommand.Type = CommandType.LargeOperation;
			newCommand.Description = "Assign everyone a role identified by it's ID. Use the `getRole` command to get the ID.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				SocketRole role = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) ||
				    !guid.TryParse(e.TrimmedMessage, out guid id) || id < int.MaxValue ||
				    (role = e.Server.Guild.Roles.FirstOrDefault(r => r.Id == id)) == null )
				{
					await e.SendReplySafe(ErrorRoleNotFoundId);
					return;
				}

				await e.SendReplySafe(PromoteEveryoneResponseString);
				await e.Server.Guild.DownloadUsersAsync();
				List<SocketGuildUser> users = e.Server.Guild.Users.ToList();

				int i = 0;
				int count = 0;
				int exceptions = 0;
				bool canceled = await e.Operation.While(() => i < users.Count, async () => {
					try
					{
						SocketGuildUser user = users[i++];
						if( user.Roles.Any(r => r.Id == role.Id) )
							return false;

						await user.AddRoleAsync(role);
						count++;
					}
					catch(Exception)
					{
						if( ++exceptions > 10 )
							return true;
					}

					return false;
				});

				if( canceled )
					return;

				string response = exceptions > 10 ? ErrorPromoteEveryone : ($"Done! I've assigned `{role.Name}` to `{count}` member" + (count != 1 ? "s." : "."));
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !demoteEveryone
			newCommand = new Command("demoteEveryone");
			newCommand.Type = CommandType.LargeOperation;
			newCommand.Description = "Remove a role from everyone, identified by roleID. Use the `getRole` command to get the ID.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				SocketRole role = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) ||
				    !guid.TryParse(e.TrimmedMessage, out guid id) || id < int.MaxValue ||
				    (role = e.Server.Guild.Roles.FirstOrDefault(r => r.Id == id)) == null )
				{
					await e.SendReplySafe(ErrorRoleNotFoundId);
					return;
				}

				await e.SendReplySafe(DemoteEveryoneResponseString);
				await e.Server.Guild.DownloadUsersAsync();
				List<SocketGuildUser> users = e.Server.Guild.Users.ToList();

				int i = 0;
				int count = 0;
				int exceptions = 0;
				bool canceled = await e.Operation.While(() => i < users.Count, async () => {
					try
					{
						SocketGuildUser user = users[i++];
						if( !user.Roles.Any(r => r.Id == role.Id) )
							return false;

						await user.RemoveRoleAsync(role);
						count++;
					}
					catch(Exception)
					{
						if( ++exceptions > 10 )
							return true;
					}

					return false;
				});

				if( canceled )
					return;

				string response = exceptions > 10 ? ErrorPromoteEveryone : ($"Done! I've removed `{role.Name}` from `{count}` member" + (count != 1 ? "s." : "."));
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !countWithoutRoles
			newCommand = new Command("countWithoutRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Count how many users do not have any role.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				await e.SendReplySafe(WorkingOnItString);
				await e.Server.Guild.DownloadUsersAsync();
				int count = e.Server.Guild.Users.Count(u => !u.IsBot && u.Roles.Any(r => r.Id != e.Server.Id));
				await e.SendReplySafe(string.Format(FoundCountString, count));
			};
			commands.Add(newCommand);

// !kickWithoutRoles
			newCommand = new Command("kickWithoutRoles");
			newCommand.Type = CommandType.LargeOperation;
			newCommand.Description = "Kick all the users who do not have any role.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.KickMembers )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				await e.SendReplySafe(WorkingOnItString);
				await e.Server.Guild.DownloadUsersAsync();
				List<SocketGuildUser> users = e.Server.Guild.Users.ToList();

				int i = 0;
				int count = 0;
				int exceptions = 0;
				bool canceled = await e.Operation.While(() => i < users.Count, async () => {
					try
					{
						SocketGuildUser user = users[i++];
						if( user.IsBot || user.Roles.Any(r => r.Id != e.Server.Id) )
							return false;

						await user.KickAsync();
						count++;
					}
					catch(Exception)
					{
						if( ++exceptions > 10 )
							return true;
					}

					return false;
				});

				if( canceled )
					return;

				string response = exceptions > 10 ? ErrorKickWithoutRole : $"Done! I've kicked `{count}` members who had no roles! You know what to press to pay respects!";
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);


			return commands;
		}

		private async Task OnUserJoined(SocketGuildUser user)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(user.Guild.Id) || (server = this.Client.Servers[user.Guild.Id]) == null )
				return;

			if( server.Config.WelcomeMessageEnabled && !string.IsNullOrWhiteSpace(server.Config.WelcomeMessage) )
			{
				try
				{
					string welcomePm = server.Config.WelcomeMessage;
					if( server.Config.WelcomeMessage.Contains("{0}") )
						welcomePm = string.Format(server.Config.WelcomeMessage, user.Username);
					await user.SendMessageSafe(welcomePm);
				} catch(Exception) { }
			}

			SocketRole role;
			if( server.Config.WelcomeRoleId != 0 && (role = server.Guild.GetRole(server.Config.WelcomeRoleId)) != null )
			{
				try
				{
					await user.AddRoleAsync(role);
				} catch(Exception) { }
			}
		}

		public async Task OnReactionAdded(IUserMessage message, ISocketMessageChannel iChannel, SocketReaction reaction)
		{
			await ReactionAssignedRoles(reaction, true);
		}

		public async Task OnReactionRemoved(IUserMessage message, ISocketMessageChannel iChannel, SocketReaction reaction)
		{
			await ReactionAssignedRoles(reaction, false);
		}

		public async Task ReactionAssignedRoles(SocketReaction reaction, bool assignRoles)
		{
			Server server;
			if( !(reaction.Channel is SocketTextChannel channel) || !this.Client.Servers.ContainsKey(channel.Guild.Id) || (server = this.Client.Servers[channel.Guild.Id]) == null || server.Config == null )
				return;

			IEnumerable<ReactionAssignedRole> roles;
			lock(server.ReactionRolesLock)
			{
				roles = server.ReactionAssignedRoles.Where(r => r.MessageId == reaction.MessageId && r.Emoji == reaction.Emote.Name).ToList();
			}

			if( roles.Any() )
			{
				SocketGuildUser user = null;
				if( !reaction.User.IsSpecified || (user = reaction.User.Value as SocketGuildUser) == null )
				{
					user = server.Guild.GetUser(reaction.UserId);
					if( user == null )
						return;
				}

				try
				{
					foreach( ReactionAssignedRole role in roles )
					{
						if( assignRoles == user.Roles.Any(r => r.Id == role.RoleId) )
							continue;

						IRole discordRole = server.Guild.GetRole(role.RoleId);
						if( discordRole == null )
							continue;

						if( !assignRoles )
						{
							await user.RemoveRoleAsync(discordRole);
							return;
						}
						//else...
						Int64 groupId = server.Roles.ContainsKey(discordRole.Id) ? server.Roles[discordRole.Id].PublicRoleGroupId : 0;
						if( groupId != 0 )
						{
							List<guid> groupRoleIds = server.Roles.Where(r => r.Value.PermissionLevel == RolePermissionLevel.Public && r.Value.PublicRoleGroupId == groupId).Select(r => r.Value.RoleId).ToList();
							int userHasCount = user.Roles.Count(r => groupRoleIds.Any(id => id == r.Id));

							RoleGroupConfig groupConfig = null;
							if( userHasCount > 0 )
							{
								ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
								groupConfig = dbContext.PublicRoleGroups.FirstOrDefault(g => g.ServerId == server.Id && g.GroupId == groupId);
								dbContext.Dispose();

								while( userHasCount >= groupConfig.RoleLimit && groupRoleIds.Any() )
								{
									IRole roleToRemove = server.Guild.GetRole(groupRoleIds.Last());
									groupRoleIds.Remove(groupRoleIds.Last());
									if( roleToRemove == null || user.Roles.All(r => r.Id != roleToRemove.Id) )
										continue;

									await user.RemoveRoleAsync(roleToRemove);
									userHasCount--;
								}
							}
						}

						await user.AddRoleAsync(discordRole);
					}
				}
				catch(Exception e)
				{
					await this.HandleException(e, "Reaction Assigned Roles", server.Id);
				}
			}
		}

		public async Task Update(IBotwinderClient iClient)
		{
			if( !this.Client.GlobalConfig.ModuleUpdateEnabled )
				return;

			BotwinderClient client = iClient as BotwinderClient;
			ServerContext dbContext = ServerContext.Create(client.DbConnectionString);

			List<RoleConfig> rolesToRemove = new List<RoleConfig>();
			foreach( RoleConfig roleConfig in dbContext.Roles.Where(r => r.DeleteAtTime > DateTime.MinValue + TimeSpan.FromMinutes(1) && r.DeleteAtTime < DateTime.UtcNow) )
			{
				Server server;
				if( !client.Servers.ContainsKey(roleConfig.ServerId) ||
				    (server = client.Servers[roleConfig.ServerId]) == null )
					continue;

				try
				{
					SocketRole role = server.Guild.GetRole(roleConfig.RoleId);
					if(role != null && !role.Deleted)
						await role.DeleteAsync();

					rolesToRemove.Add(roleConfig);
				}
				catch(Exception e)
				{
					await this.HandleException(e, "Delete Temporary Role failed.", roleConfig.ServerId);
				}
			}

			if( rolesToRemove.Any() )
			{
				dbContext.Roles.RemoveRange(rolesToRemove);
				dbContext.SaveChanges();
			}

			dbContext.Dispose();
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
