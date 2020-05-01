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
		private static string FoundCountString = "There are `{0}` members without any role.";

		private ValkyrjaClient Client;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.UserJoined += OnUserJoined;
			this.Client.Events.ReactionAdded += OnReactionAdded;
			this.Client.Events.ReactionRemoved += OnReactionRemoved;

// !publicRoles
			Command newCommand = new Command("publicRoles");
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
				Dictionary<Int64, RoleGroupConfig> groupConfigs = dbContext.PublicRoleGroups.AsQueryable().Where(g => g.ServerId == e.Server.Id).ToDictionary(g => g.GroupId);
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
					string limitVerbose = groupConfig.RoleLimit == 0 ? "any" : groupConfig.RoleLimit.ToString();
					responseBuilder.Append($"\n\n**{name}** - you can join {limitVerbose} of these:\n");

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
				IEnumerable<RoleGroupConfig> roleGroups = dbContext.PublicRoleGroups.AsQueryable().Where(g => g.ServerId == e.Server.Id);
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
						groupConfig = dbContext.PublicRoleGroups.AsQueryable().FirstOrDefault(g => g.ServerId == e.Server.Id && g.GroupId == groupId);
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
				} catch(HttpException exception)
				{
					await e.Server.HandleHttpException(exception, $"This happened in <#{e.Channel.Id}> when executing command `{e.CommandId}`");
					response = Utils.HandleHttpException(exception);
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
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
					await (e.Message.Author as SocketGuildUser)?.RemoveRoleAsync(foundRoles.First());
				} catch(HttpException exception)
				{
					await e.Server.HandleHttpException(exception, $"This happened in <#{e.Channel.Id}> when executing command `{e.CommandId}`");
					response = Utils.HandleHttpException(exception);
				} catch(Exception exception)
				{
					await this.Client.LogException(exception, e);
					response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
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

// !promoteEveryone
			newCommand = new Command("promoteEveryone");
			newCommand.Type = CommandType.Operation;
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
			newCommand.Type = CommandType.Operation;
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
				int count = e.Server.Guild.Users.Count(u => !u.IsBot && u.Roles.All(r => r.Id == e.Server.Id));
				await e.SendReplySafe(string.Format(FoundCountString, count));
			};
			commands.Add(newCommand);

// !kickWithoutRoles
			newCommand = new Command("kickWithoutRoles");
			newCommand.Type = CommandType.Operation;
			newCommand.Description = "Kick all the users who do not have any role.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner;
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
						if( user.IsBot || e.Server.Guild.OwnerId == user.Id || user.Roles.Any(r => r.Id != e.Server.Id) )
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
				string welcomePm = server.Config.WelcomeMessage;
				if( server.Config.WelcomeMessage.Contains("{0}") )
					welcomePm = string.Format(server.Config.WelcomeMessage, user.Username);
				await this.Client.SendPmSafe(user, welcomePm);
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

				string name = "unknown";
				try
				{
					foreach( ReactionAssignedRole role in roles )
					{
						if( assignRoles == user.Roles.Any(r => r.Id == role.RoleId) )
							continue;

						IRole discordRole = server.Guild.GetRole(role.RoleId);
						if( discordRole == null )
							continue;

						name = discordRole.Name;

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
								groupConfig = dbContext.PublicRoleGroups.AsQueryable().FirstOrDefault(g => g.ServerId == server.Id && g.GroupId == groupId);
								dbContext.Dispose();

								while( userHasCount >= groupConfig.RoleLimit && groupRoleIds.Any() )
								{
									IRole roleToRemove = server.Guild.GetRole(groupRoleIds.Last());
									groupRoleIds.Remove(groupRoleIds.Last());
									if( roleToRemove == null || user.Roles.All(r => r.Id != roleToRemove.Id) )
										continue;

									await user.RemoveRoleAsync(roleToRemove);
									try
									{
										if( await reaction.Channel.GetMessageAsync(reaction.MessageId) is SocketUserMessage msg )
											await msg.RemoveReactionAsync(reaction.Emote, reaction.UserId);
									}
									catch( Exception e )
									{
										await this.HandleException(e, "Failed to remove reaction.", server.Id);
									}

									userHasCount--;
								}
							}
						}

						await user.AddRoleAsync(discordRole);
					}
				}
				catch( HttpException e )
				{
					await server.HandleHttpException(e, $"This happened in <#{channel.Id}> when trying to change reactions or assign roles based on emojis.");
				}
				catch( Exception e )
				{
					await this.HandleException(e, "Reaction Assigned Roles", server.Id);
				}
			}
		}

		public async Task Update(IValkyrjaClient iClient)
		{
			if( !this.Client.GlobalConfig.ModuleUpdateEnabled )
				return;

			ValkyrjaClient client = iClient as ValkyrjaClient;
			ServerContext dbContext = ServerContext.Create(client.DbConnectionString);

			List<RoleConfig> rolesToRemove = new List<RoleConfig>();
			foreach( RoleConfig roleConfig in dbContext.Roles.AsQueryable().Where(r => r.DeleteAtTime > DateTime.MinValue + TimeSpan.FromMinutes(1) && r.DeleteAtTime < DateTime.UtcNow) )
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
	}
}
