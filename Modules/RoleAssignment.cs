using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
	public class RoleAssignment: IModule
	{
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		private static string ErrorNoPublicRoles = "I'm sorry, but there are no public roles on this server.";
		private static string ErrorNoMemberRoles = "I'm sorry, but there are no member roles on this server.";
		private static string ErrorTooManyFound = "I found more than one role with that expression, please be more specific.";
		private static string ErrorRoleNotFound = "I did not find a role based on that expression.";
		private static string ErrorRoleNotFoundId = "Role not found. Use with roleID parameter.\n(Use the `getRole` command to get the ID)";
		private static string ErrorPromoteEveryone = "Failed to assign the role to more than 10 people - aborting. (Assuming wrong permissions or hierarchy.)";
		private static string PromoteEveryoneResponseString = "I will assign a role to everyone, which may take **very** long time. Please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";
		private static string DemoteEveryoneResponseString = "I will remove a role from everyone, which may take **very** long time. Please be patient.\n_(You can check using `operations` and you can also `cancel` it.)_";

		private BotwinderClient Client;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.UserJoined += OnUserJoined;

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
					await iClient.SendMessageToChannel(e.Channel, "Role not found.");
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

				await iClient.SendMessageToChannel(e.Channel, response.ToString());
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
					await iClient.SendMessageToChannel(e.Channel, ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorTooManyFound);
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
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("listMembers"));

// !createRole
			newCommand = new Command("createRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Create a role with specified name.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, "What role? Name? Do you want me to come up with something silly or what?");
					return;
				}

				RestRole role = await e.Server.Guild.CreateRoleAsync(e.TrimmedMessage, GuildPermissions.None);
				string response = $"Role created: `{role.Name}`\n  Id: `{role.Id}`";
				await iClient.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);

// !publicRoles
			newCommand = new Command("publicRoles");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "See what Public Roles can you join on this server.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorNoPublicRoles);
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

					responseBuilder.Append(GetRoleNames(group.Value));
				}

				if( hadGroup )
				{
					responseBuilder.AppendLine("\n\n_(Where the `Group` roles are mutually exclusive - joining a `Group` role will remove any other role out of that group, that you already have.)_");
				}

				await iClient.SendMessageToChannel(e.Channel, responseBuilder.ToString());
			};
			commands.Add(newCommand);

// !join
			newCommand = new Command("join");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Use with parameter, name of a Role that you wish to join.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorPermissionsString);
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, e.Command.Description);
					return;
				}

				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorNoPublicRoles);
					return;
				}

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => publicRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == e.TrimmedMessage)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == e.TrimmedMessage.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(e.TrimmedMessage.ToLower()))).Any() )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorTooManyFound);
					return;
				}

				IEnumerable<guid> idsToLeave = null;
				SocketRole roleToAssign = foundRoles.First();
				Int64 groupId = publicRoles.First(r => r.RoleId == roleToAssign.Id).PublicRoleGroupId;

				if( groupId != 0 )
				{
					idsToLeave = publicRoles.Where(r => r.PublicRoleGroupId == groupId).Select(r => r.RoleId);
				}

				bool removed = false;
				string response = "Done!";
				try
				{
					SocketGuildUser user = (e.Message.Author as SocketGuildUser);

					if( idsToLeave != null )
					{
						foreach( guid id in idsToLeave )
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
					if( exception is Discord.Net.HttpException ex && ex.HttpCode == System.Net.HttpStatusCode.Forbidden ||
					    exception.Message.Contains("Missing Access") )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				if( removed )
					response += "\n_(I've removed the other exclusive roles from the same role group.)_";

				await iClient.SendMessageToChannel(e.Channel, response);

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
					await iClient.SendMessageToChannel(e.Channel, ErrorPermissionsString);
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await iClient.SendMessageToChannel(e.Channel, e.Command.Description);
					return;
				}

				List<RoleConfig> publicRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Public).ToList();
				if( publicRoles == null || publicRoles.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorNoPublicRoles);
					return;
				}

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => publicRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == e.TrimmedMessage)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == e.TrimmedMessage.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(e.TrimmedMessage.ToLower()))).Any() )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorTooManyFound);
					return;
				}

				string response = "Done!";
				try
				{
					await (e.Message.Author as SocketGuildUser).RemoveRoleAsync(foundRoles.First());
				} catch(Exception exception)
				{
					if( exception is Discord.Net.HttpException ex && ex.HttpCode == System.Net.HttpStatusCode.Forbidden ||
					    exception.Message.Contains("Missing Access") )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				await iClient.SendMessageToChannel(e.Channel, response);

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
					await iClient.SendMessageToChannel(e.Channel, ErrorNoMemberRoles);
					return;
				}

				string response = string.Format("You can use `{0}promote` and `{0}demote` commands with these Member Roles: {1}",
					e.Server.Config.CommandPrefix,
					e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc.RoleId == r.Id)).Select(r => r.Name).ToNames()
				);

				await iClient.SendMessageToChannel(e.Channel, response);
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
					await iClient.SendMessageToChannel(e.Channel, ErrorPermissionsString);
					return;
				}

				List<SocketGuildUser> users;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !(users = this.Client.GetMentionedGuildUsers(e)).Any() || e.MessageArgs.Length <= users.Count )
				{
					await iClient.SendMessageToChannel(e.Channel, e.Command.Description);
					return;
				}

				List<RoleConfig> memberRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).ToList();
				if( memberRoles == null || memberRoles.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorNoMemberRoles);
					return;
				}

				string expression = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[users.Count]));

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == expression)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == expression.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(expression.ToLower()))).Any() )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorTooManyFound);
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
					if( exception is Discord.Net.HttpException ex && ex.HttpCode == System.Net.HttpStatusCode.Forbidden ||
					    exception.Message.Contains("Missing Access") )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				await iClient.SendMessageToChannel(e.Channel, response);
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
					await iClient.SendMessageToChannel(e.Channel, ErrorPermissionsString);
					return;
				}

				List<SocketGuildUser> users;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || !(users = this.Client.GetMentionedGuildUsers(e)).Any() || e.MessageArgs.Length <= users.Count)
				{
					await iClient.SendMessageToChannel(e.Channel, e.Command.Description);
					return;
				}

				List<RoleConfig> memberRoles = e.Server.Roles.Values.Where(r => r.PermissionLevel == RolePermissionLevel.Member).ToList();
				if( memberRoles == null || memberRoles.Count == 0 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorNoMemberRoles);
					return;
				}

				string expression = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[users.Count]));

				IEnumerable<SocketRole> roles = e.Server.Guild.Roles.Where(r => memberRoles.Any(rc => rc.RoleId == r.Id));
				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = roles.Where(r => r.Name == expression)).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower() == expression.ToLower())).Any() &&
				    !(foundRoles = roles.Where(r => r.Name.ToLower().Contains(expression.ToLower()))).Any() )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorRoleNotFound);
					return;
				}

				if( foundRoles.Count() > 1 )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorTooManyFound);
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
					if( exception is Discord.Net.HttpException ex && ex.HttpCode == System.Net.HttpStatusCode.Forbidden ||
						exception.Message.Contains("Missing Access") )
						response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
					else
					{
						await this.Client.LogException(exception, e);
						response = $"Unknown error, please poke <@{this.Client.GlobalConfig.AdminUserId}> to take a look x_x";
					}
				}

				await iClient.SendMessageToChannel(e.Channel, response);
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
					await iClient.SendMessageToChannel(e.Channel, ErrorPermissionsString);
					return;
				}

				SocketRole role = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) ||
				    !guid.TryParse(e.TrimmedMessage, out guid id) || id < int.MaxValue ||
				    (role = e.Server.Guild.Roles.FirstOrDefault(r => r.Id == id)) == null )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorRoleNotFoundId);
					return;
				}

				await iClient.SendMessageToChannel(e.Channel, PromoteEveryoneResponseString);
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
				await iClient.SendMessageToChannel(e.Channel, response);
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
					await iClient.SendMessageToChannel(e.Channel, ErrorPermissionsString);
					return;
				}

				SocketRole role = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) ||
				    !guid.TryParse(e.TrimmedMessage, out guid id) || id < int.MaxValue ||
				    (role = e.Server.Guild.Roles.FirstOrDefault(r => r.Id == id)) == null )
				{
					await iClient.SendMessageToChannel(e.Channel, ErrorRoleNotFoundId);
					return;
				}

				await iClient.SendMessageToChannel(e.Channel, DemoteEveryoneResponseString);
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
				await iClient.SendMessageToChannel(e.Channel, response);
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

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
