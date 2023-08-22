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
		private static string ErrorDemoteEveryone = "Failed to remove the role from more than 10 people - aborting. (Assuming wrong permissions or hierarchy.)";
		private static string ErrorKickWithoutRole = "Failed to kick more than 10 people - aborting. (Assuming wrong permissions, hierarchy, or Discord derps much.)";
		private static string PromoteEveryoneResponseString = "I will assign a role to everyone, which may take **very** long time. Please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";
		private static string DemoteEveryoneResponseString = "I will remove a role from everyone, which may take **very** long time. Please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";
		private static string WorkingOnItString = "Working on it. This may take some time so please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";
		private static string FoundCountString = "There are `{0}` members without any role.";

		private ValkyrjaClient Client;

		private readonly List<guid> ReactionUsers = new List<guid>();

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.UserJoined += OnUserJoined;
			this.Client.Events.UserUpdated += OnUserUpdated;
			this.Client.Events.ReactionAdded += OnReactionAdded;
			this.Client.Events.ReactionRemoved += OnReactionRemoved;
			this.Client.Events.DropdownSelected += OnDropdownSelected;

			this.Client.DiscordClient.ButtonExecuted += FixRoles;

// !publicRoles
			Command newCommand = new Command("publicRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "See what Public Roles you can join on this server.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoPublicRoles);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				StringBuilder responseBuilder = new StringBuilder(e.Server.Localisation.GetString("role_publicroles_print", e.Server.Config.CommandPrefix));
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
					responseBuilder.Append(e.Server.Localisation.GetString("role_publicroles_group", name, limitVerbose));

					responseBuilder.Append(GetRoleNames(groupRole.Value));
				}

				await e.SendReplySafe(responseBuilder.ToString());
			};
			commands.Add(newCommand);

// !roleCounts
			newCommand = new Command("roleCounts");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Get some numbers about public roles for specific group.";
			newCommand.ManPage = new ManPage("<expression>", "`<expression>` - An expression using which to search for a role group.");
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
			newCommand.Description = "Grab a public role for yourself.";
			newCommand.ManPage = new ManPage("<expression>", "`<expression>` - An expression using which to search for a public role.");
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(e.Command.ManPage.ToString(e.Server.Config.CommandPrefix + e.CommandId));
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
					await e.SendReplySafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplySafe(ErrorTooManyFound);
					return;
				}

				RoleGroupConfig groupConfig = null;
				IEnumerable<guid> groupRoleIds = null;
				SocketRole roleToAssign = foundRoles.First();
				RoleConfig roleConfig = publicRoles.First(r => r.RoleId == roleToAssign.Id);
				Int64 groupId = roleConfig.PublicRoleGroupId;

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
							await e.SendReplySafe($"You can only have {groupConfig.RoleLimit} roles from the `{groupConfig.Name}` group.");
							return;
						}
					}
				}

				bool removed = false;
				string response = e.Server.Localisation.GetString("role_join_done");
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

					ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
					if( dbContext.GetOrAddUser(e.Server.Id, user.Id).IsAllowedRole(roleConfig) )
						await user.AddRoleAsync(roleToAssign);
					else
						response = e.Server.Localisation.GetString("role_join_denied");
					dbContext.Dispose();

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
					response += e.Server.Localisation.GetString("role_join_exclusiveremoved");

				await e.SendReplySafe(response);

				if( this.Client.Events.LogPublicRoleJoin != null )
					await this.Client.Events.LogPublicRoleJoin(e.Server, e.Message.Author as SocketGuildUser, roleToAssign.Name);
			};
			commands.Add(newCommand);

// !leave
			newCommand = new Command("leave");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Dispose of your public role.";
			newCommand.ManPage = new ManPage("<expression>", "`<expression>` - An expression using which to search for a public role.");
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(e.Command.ManPage.ToString(e.Server.Config.CommandPrefix + e.CommandId));
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
					await e.SendReplySafe(ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await e.SendReplySafe(ErrorTooManyFound);
					return;
				}

				string response = e.Server.Localisation.GetString("role_leave_done");
				try
				{
					SocketRole roleToRemove = foundRoles.First();
					SocketGuildUser user = e.Message.Author as SocketGuildUser ?? throw new NullReferenceException("Author is not a SocketGuildUser");
					await user.RemoveRoleAsync(roleToRemove);
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

				if( this.Client.Events.LogPublicRoleLeave != null )
					await this.Client.Events.LogPublicRoleLeave(e.Server, e.Message.Author as SocketGuildUser, foundRoles.First().Name);
			};
			commands.Add(newCommand);

// !memberRoles
			newCommand = new Command("memberRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "See what Member Roles you can assign to others.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				SocketGuildUser moderator = e.Message.Author as SocketGuildUser;
				if( moderator == null )
				{
					await this.HandleException(new NullReferenceException("Message author is not a SocketGuildUser"), e.Message.Author.Id.ToString(), e.Server.Id);
					return;
				}

				List<guid> memberRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).Select(r => r.RoleId).ToList();
				memberRoles.AddRange(e.Server.CategoryMemberRoles.Where(rc => moderator.Roles.Any(r => r.Id == rc.ModRoleId)).Select(r => r.MemberRoleId));
				if( memberRoles == null || memberRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoMemberRoles);
					return;
				}

				string response = e.Server.Localisation.GetString("role_memberroles_print",
					e.Server.Config.CommandPrefix,
					e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc == r.Id)).Select(r => r.Name).ToNames()
				);

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !promote
			newCommand = new Command("promote");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Assign someone a member role.";
			newCommand.ManPage = new ManPage("<@users> <expression>", "`<@users>` - User mention(s) (or IDs) whom to assign the member role.\n\n`<expression>` - An expression using which to search for a member role.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				SocketGuildUser moderator = e.Message.Author as SocketGuildUser;
				if( moderator == null )
				{
					await this.HandleException(new NullReferenceException("Message author is not a SocketGuildUser"), e.Message.Author.Id.ToString(), e.Server.Id);
					return;
				}

				List<IGuildUser> users;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !(users = await this.Client.GetMentionedGuildUsers(e)).Any() || e.MessageArgs.Length <= users.Count )
				{
					await e.SendReplySafe(e.Command.ManPage.ToString(e.Server.Config.CommandPrefix + e.CommandId));
					return;
				}

				List<guid> memberRoleIds = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).Select(r => r.RoleId).ToList();
				memberRoleIds.AddRange(e.Server.CategoryMemberRoles.Where(rc => moderator.Roles.Any(r => r.Id == rc.ModRoleId)).Select(r => r.MemberRoleId));

				if( memberRoleIds == null || memberRoleIds.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoMemberRoles);
					return;
				}

				string expression = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[users.Count]));

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => memberRoleIds.Any(rc => rc == r.Id));
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

				string response = e.Server.Localisation.GetString("role_promote_done");
				SocketRole role = foundRoles.First();
				try
				{
					foreach( IGuildUser user in users )
					{
						await user.AddRoleAsync(role);

						RoleConfig roleConfig = e.Server.Roles.Values.FirstOrDefault(r => r.RoleId == role.Id);
						if( roleConfig != null && roleConfig.PersistenceUserFlag > 0 )
						{
							ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
							UserData userData = dbContext.GetOrAddUser(e.Server.Id, user.Id);
							userData.AssignPersistence(roleConfig);
							dbContext.SaveChanges();
							dbContext.Dispose();
						}

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
			newCommand.Description = "Remove a member role from someone.";
			newCommand.ManPage = new ManPage("<@users> <expression>", "`<@users>` - User mention(s) (or IDs) whom to remove the member role.\n\n`<expression>` - An expression using which to search for a member role.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				SocketGuildUser moderator = e.Message.Author as SocketGuildUser;
				if( moderator == null )
				{
					await this.HandleException(new NullReferenceException("Message author is not a SocketGuildUser"), e.Message.Author.Id.ToString(), e.Server.Id);
					return;
				}

				List<IGuildUser> users;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !(users = await this.Client.GetMentionedGuildUsers(e)).Any() || e.MessageArgs.Length <= users.Count)
				{
					await e.SendReplySafe(e.Command.ManPage.ToString(e.Server.Config.CommandPrefix + e.CommandId));
					return;
				}

				List<guid> memberRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).Select(r => r.RoleId).ToList();
				memberRoles.AddRange(e.Server.CategoryMemberRoles.Where(rc => moderator.Roles.Any(r => r.Id == rc.ModRoleId)).Select(r => r.MemberRoleId));

				if( memberRoles == null || memberRoles.Count == 0 )
				{
					await e.SendReplySafe(ErrorNoMemberRoles);
					return;
				}

				string expression = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[users.Count]));

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc == r.Id));
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
				string response = e.Server.Localisation.GetString("role_demote_done");
				try
				{
					foreach( IGuildUser user in users )
					{
						await user.RemoveRoleAsync(role);

						RoleConfig roleConfig = e.Server.Roles.Values.FirstOrDefault(r => r.RoleId == role.Id);
						if( roleConfig != null && roleConfig.PersistenceUserFlag > 0 )
						{
							ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
							UserData userData = dbContext.GetOrAddUser(e.Server.Id, user.Id);
							userData.RemovePersistence(roleConfig);
							dbContext.SaveChanges();
							dbContext.Dispose();
						}

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

// !categoryMembers
			newCommand = new Command("categoryMembers");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Configure category member roles.";
			newCommand.ManPage = new ManPage("<add> <modRoleId> <memberRoleId> | <reset> <modRoleId> | <resetAll>", "`<add>` - An instruction to add the following roles as a moderator who can assign the member role.\n\n`<reset>` - An instruction to clear all the member roles associated with the specified moderator role ID.\n\n`<modRoleId>` - Moderator role ID to be added or reset. \n\n`<memberRoleId>` Member role ID to be added.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				string response = "Invalid arguments:\n" + e.Command.ManPage.ToString(e.Server.Config.CommandPrefix + e.CommandId);

				if( e.MessageArgs == null || e.MessageArgs.Length < 1 )
				{
					await e.SendReplySafe(response);
					return;
				}

				guid modRoleId;
				IEnumerable<CategoryMemberRole> rolesToRemove;
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				switch( e.MessageArgs[0].ToLower() )
				{
					case "add":
						if( !guid.TryParse(e.MessageArgs[1], out modRoleId) || !guid.TryParse(e.MessageArgs[2], out guid memberRoleId) )
							break;
						dbContext.GetOrAddMemberRole(e.Server.Id, modRoleId, memberRoleId);
						dbContext.SaveChanges();

						response = "Sure.";
						break;
					case "reset":
						if( !guid.TryParse(e.MessageArgs[1], out modRoleId) )
							break;
						rolesToRemove = dbContext.CategoryMemberRoles.AsQueryable().Where(r => r.ServerId == e.Server.Id && r.ModRoleId == modRoleId);
						if( rolesToRemove.Any() )
						{
							dbContext.CategoryMemberRoles.RemoveRange(rolesToRemove);
							dbContext.SaveChanges();
						}

						response = "Sure.";
						break;
					case "resetAll":
						rolesToRemove = dbContext.CategoryMemberRoles.AsQueryable().Where(r => r.ServerId == e.Server.Id);
						if( rolesToRemove.Any() )
						{
							dbContext.CategoryMemberRoles.RemoveRange(rolesToRemove);
							dbContext.SaveChanges();
						}

						response = "Sure.";
						break;
					default:
						break;
				}

				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !promoteEveryone
			newCommand = new Command("promoteEveryone");
			newCommand.Type = CommandType.Operation;
			newCommand.Description = "Assign everyone a role identified by it's ID. Use the `getRole` command to get the ID.";
			newCommand.ManPage = new ManPage("<roleID>", "`<roleID>` - Specific Role ID of a role.");
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
			newCommand.ManPage = new ManPage("<roleID>", "`<roleID>` - Specific Role ID of a role.");
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

				string response = exceptions > 10 ? ErrorDemoteEveryone : ($"Done! I've removed `{role.Name}` from `{count}` member" + (count != 1 ? "s." : "."));
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);

// !countWithoutRoles
			newCommand = new Command("countWithoutRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Count how many users do not have any role.";
			newCommand.ManPage = new ManPage("", "");
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
			newCommand.ManPage = new ManPage("", "");
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

		private async Task FixRoles(SocketMessageComponent component)
		{
			try
			{
				Server server = null;
				if( component.Data.CustomId != "fix-roles" || !component.GuildId.HasValue || !this.Client.Servers.ContainsKey(component.GuildId.Value) || (server = this.Client.Servers[component.GuildId.Value]) == null )
					return;

				await OnUserJoinedRoles(server, component.User as SocketGuildUser);

				await component.RespondAsync("Done, contact the Moderators if you're still missing something.", ephemeral: true);
			}
			catch( Exception exception )
			{
				await this.HandleException(exception, "FixRolesCommand", 0);
			}
		}

		private async Task OnUserJoined(SocketGuildUser user)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(user.Guild.Id) || (server = this.Client.Servers[user.Guild.Id]) == null )
				return;

			if( server.Config.VerifyRoleId != 0 && server.Guild.GetRole(server.Config.VerifyRoleId) != null && (server.Config.VerifyOnWelcome || server.Config.CaptchaVerificationEnabled || server.Config.CodeVerificationEnabled) )
				return;

			await OnUserJoinedRoles(server, user);
		}

		private async Task OnUserUpdated(SocketUser sOldUser, SocketUser sUser)
		{
			Server server;
			if( sOldUser is not IGuildUser oldUser || sUser is not SocketGuildUser user ||
			    !this.Client.Servers.ContainsKey(user.Guild.Id) || (server = this.Client.Servers[user.Guild.Id]) == null )
				return;

			if( server.Config.VerifyRoleId != 0 && server.Guild.GetRole(server.Config.VerifyRoleId) != null && (server.Config.VerifyOnWelcome || server.Config.CaptchaVerificationEnabled || server.Config.CodeVerificationEnabled) &&
			    oldUser.RoleIds.All(rId => rId != server.Config.VerifyRoleId) && user.Roles.Any(r => r.Id == server.Config.VerifyRoleId) )
				await OnUserJoinedRoles(server, user);
		}

		private async Task OnUserJoinedRoles(Server server, SocketGuildUser user)
		{
			if( user == null )
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
				}
				catch( HttpException ex )
				{
					await server.HandleHttpException(ex, $"Failed to assign a Welcome role.");
				}
				catch( Exception ex )
				{
					await this.HandleException(ex, "RoleAssignment - welcome role assignment", server.Id);
				}
			}

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
			UserData userData = dbContext.GetOrAddUser(server.Id, user.Id);
			//Either InversePersistence or user has specific role flag => assign the role.
			try
			{
				await user.AddRolesAsync(server.Roles.Values.Where(r => 
					r.PersistenceUserFlag > 0 && (r.InversePersistence ^ ((userData.PersistenceFlags & (1 << (int)r.PersistenceUserFlag)) > 0))
				).Select(r => r.RoleId));
			}
			catch( HttpException ex )
			{
				await server.HandleHttpException(ex, $"Failed to assign a Persistent role.");
			}
			catch( Exception ex )
			{
				await this.HandleException(ex, "RoleAssignment - persistent role assignment", server.Id);
			}
			dbContext.Dispose();
		}

		public async Task OnReactionAdded(IUserMessage message, IMessageChannel iChannel, SocketReaction reaction)
		{
			await ReactionAssignedRoles(reaction, true);
		}

		public async Task OnReactionRemoved(IUserMessage message, IMessageChannel iChannel, SocketReaction reaction)
		{
			await ReactionAssignedRoles(reaction, false);
		}

		public async Task OnDropdownSelected(SocketMessageComponent component)
		{
			Server server;
			IGuildUser user = component.User as IGuildUser;
			if( !component.GuildId.HasValue || !this.Client.Servers.ContainsKey(component.GuildId.Value) || (server = this.Client.Servers[component.GuildId.Value]) == null || server.Config == null || user == null )
				return;

			List<guid> roleIdsToAssign = new List<ulong>();
			List<guid> roleIdsToRemove = new List<ulong>();
			string reply = null;

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
			foreach( string stringValue in component.Data.Values )
			{
				RoleConfig roleConfig = null;
				if( !guid.TryParse(stringValue, out guid roleId) || roleId == 0 || server.Guild.GetRole(roleId) == null || !server.Roles.ContainsKey(roleId) ||  (roleConfig = server.Roles[roleId]) == null || server.Roles[roleId].PermissionLevel != RolePermissionLevel.Public )
					continue;

				if( dbContext.GetOrAddUser(server.Id, user.Id).IsAllowedRole(roleConfig) )
					roleIdsToAssign.Add(roleId);
				else
					reply = "You're not allowed to have that role.";

				Int64 groupId = roleConfig.PublicRoleGroupId;
				if( groupId != 0 )
					roleIdsToRemove.AddRange(server.Roles.Where(r => r.Value.PermissionLevel == RolePermissionLevel.Public && r.Value.PublicRoleGroupId == groupId)
						.Select(r => r.Value.RoleId).Where(rId => user.RoleIds.Contains(rId)));
			}

			if( !roleIdsToAssign.Any() )
			{
				RoleGroupConfig groupConfig = null;
				groupConfig = dbContext.PublicRoleGroups.AsQueryable().FirstOrDefault(g => g.ServerId == server.Id && g.Name == component.Data.CustomId);
				if( groupConfig != null )
				{
					roleIdsToRemove.AddRange(server.Roles.Where(r => r.Value.PermissionLevel == RolePermissionLevel.Public && r.Value.PublicRoleGroupId == groupConfig.GroupId)
						.Select(r => r.Value.RoleId).Where(rId => user.RoleIds.Contains(rId)));
				}
			}
			dbContext.Dispose();

			try
			{
				roleIdsToRemove.RemoveAll(r => roleIdsToAssign.Any(a => a == r));
				if( roleIdsToRemove.Any() )
				{
					await user.RemoveRolesAsync(roleIdsToRemove);
					reply = "Exclusive role assigned, removed your other roles.";
				}
				if( roleIdsToAssign.Any() )
					await user.AddRolesAsync(roleIdsToAssign);
			}
			catch( Exception e )
			{
				await this.HandleException(e, $"Failed to assign roles on dropdown selection: {component.Data.Values} | {roleIdsToAssign}", server.Id);
			}

			if( string.IsNullOrEmpty(reply) )
				await component.DeferAsync(true);
			else
				await component.RespondAsync(reply, ephemeral: true);
		}

		public async Task ReactionAssignedRoles(SocketReaction reaction, bool assignRoles)
		{
			Server server;
			if( !(reaction.Channel is SocketTextChannel channel) || !this.Client.Servers.ContainsKey(channel.Guild.Id) || (server = this.Client.Servers[channel.Guild.Id]) == null || server.Config == null )
				return;

			if( this.ReactionUsers.Contains(reaction.UserId) )
				return;

			server.ReactionRolesLock.Wait();
			if( this.ReactionUsers.Contains(reaction.UserId) )
			{
				server.ReactionRolesLock.Release();
				return;
			}
			this.ReactionUsers.Add(reaction.UserId);

			try
			{
				IEnumerable<ReactionAssignedRole> roles;
				roles = server.ReactionAssignedRoles.Where(r => r.MessageId == reaction.MessageId && r.Emoji == reaction.Emote.Name).ToList();

				if( roles.Any() )
				{
					IGuildUser user = null;
					if( !reaction.User.IsSpecified || (user = reaction.User.Value as SocketGuildUser) == null )
					{
						user = server.Guild.GetUser(reaction.UserId);
						if( user == null )
						{
							user = await this.Client.DiscordClient.Rest.GetGuildUserAsync(server.Id, reaction.UserId);
						}
						if( user == null )
						{
							this.ReactionUsers.Remove(reaction.UserId);
							server.ReactionRolesLock.Release();
							return;
						}
					}

					ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

					try
					{
						foreach( ReactionAssignedRole role in roles )
						{
							if( assignRoles == user.RoleIds.Any(rId => rId == role.RoleId) )
								continue;

							IRole discordRole = server.Guild.GetRole(role.RoleId);
							if( discordRole == null )
								continue;

							RoleConfig roleConfig = !server.Roles.ContainsKey(discordRole.Id) ? null : server.Roles[discordRole.Id];
							if( dbContext.GetOrAddUser(server.Id, user.Id).IsAllowedRole(roleConfig) )
								continue;

							if( !assignRoles )
							{
								await user.RemoveRoleAsync(discordRole);
								this.ReactionUsers.Remove(reaction.UserId);

								dbContext.Dispose();
								server.ReactionRolesLock.Release();
								return;
							}

							//else...
							Int64 groupId = roleConfig?.PublicRoleGroupId ?? 0;
							if( groupId != 0 )
							{
								List<guid> groupRoleIds = server.Roles.Where(r => r.Value.PermissionLevel == RolePermissionLevel.Public && r.Value.PublicRoleGroupId == groupId).Select(r => r.Value.RoleId).ToList();
								int userHasCount = user.RoleIds.Count(rId => groupRoleIds.Any(id => id == rId));

								RoleGroupConfig groupConfig = null;
								if( userHasCount > 0 )
								{
									groupConfig = dbContext.PublicRoleGroups.AsQueryable().FirstOrDefault(g => g.ServerId == server.Id && g.GroupId == groupId);

									while( userHasCount >= groupConfig.RoleLimit && groupRoleIds.Any() )
									{
										IRole roleToRemove = server.Guild.GetRole(groupRoleIds.Last());
										groupRoleIds.Remove(groupRoleIds.Last());
										if( roleToRemove == null || user.RoleIds.All(rId => rId != roleToRemove.Id) )
											continue;

										await user.RemoveRoleAsync(roleToRemove);

										try
										{
											if( await reaction.Channel.GetMessageAsync(reaction.MessageId) is SocketUserMessage sMsg )
												await sMsg.RemoveReactionAsync(reaction.Emote, reaction.UserId);
											if( await reaction.Channel.GetMessageAsync(reaction.MessageId) is RestUserMessage rMsg )
												await rMsg.RemoveReactionAsync(reaction.Emote, reaction.UserId);
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

					dbContext.Dispose();
				}
			}
			catch( Exception e )
			{
				await this.HandleException(e, "Reaction Assigned Roles", server.Id);
			}

			this.ReactionUsers.Remove(reaction.UserId);
			server.ReactionRolesLock.Release();
		}

		public Task Update(IValkyrjaClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
