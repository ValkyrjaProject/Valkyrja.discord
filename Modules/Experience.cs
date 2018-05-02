using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord.WebSocket;

using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Experience: IModule
	{
		private const string ExpDisabledString = "Experience and levels are disabled on this server.";
		private const string LevelupString = "<@{0}>, you've gained the level `{1}`";
		private const string LevelString = "Your current level is `{0}` (`{1}`)";
		private const string LevelNullString = "Your current level is `{0}`";
		private const string MessagesToLevel = "\nYou're {0} messages away from the next!";
		private const string ImagesToLevel = "\nYou're {0} images away from the next!";
		private const string ThingsToLevel = "\nYou're {0} messages or {1} images away from the next!";

		private BotwinderClient Client;
		private guid LastMessageId = 0;
		private List<guid> ServersWithException = new List<guid>();


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.MessageReceived += OnMessageReceived;

// !lvl
			Command newCommand = new Command("lvl");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Find out what's your level!";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.ExpEnabled )
				{
					await this.Client.SendMessageToChannel(e.Channel, ExpDisabledString);
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);

				string response = string.Format(LevelNullString, userData.Level);

				SocketRole role = null;
				if( userData.Level > 0 &&
				    (role = e.Server.Roles.Values.Where(r => r.ExpLevel == userData.Level)
					    .Select(r => e.Server.Guild.GetRole(r.RoleId)).FirstOrDefault()) != null )
				{
					response = string.Format(LevelString, role.Name, userData.Level);
				}

				//exp = base * lvl * (lvl + 1)
				Int64 expToLevel = e.Server.Config.BaseExpToLevelup * (userData.Level+1) * (userData.Level + 2);
				expToLevel -= userData.ExpRelative;

				if( e.Server.Config.ExpPerMessage != 0 && e.Server.Config.ExpPerAttachment != 0 )
					response += string.Format(ThingsToLevel, expToLevel / e.Server.Config.ExpPerMessage, expToLevel / e.Server.Config.ExpPerAttachment);
				else if( e.Server.Config.ExpPerMessage != 0 )
					response += string.Format(MessagesToLevel, expToLevel / e.Server.Config.ExpPerMessage);
				else if( e.Server.Config.ExpPerAttachment != 0 )
					response += string.Format(ImagesToLevel, expToLevel / e.Server.Config.ExpPerAttachment);

				await this.Client.SendMessageToChannel(e.Channel, response);
				dbContext.Dispose();
			};
			commands.Add(newCommand);


			return commands;
		}

		private async Task OnMessageReceived(SocketMessage message)
		{
			Server server;
			if( message.Author.IsBot ||
			    message.Id == LastMessageId ||
			    !(message.Channel is SocketTextChannel channel) ||
			    !this.Client.Servers.ContainsKey(channel.Guild.Id) ||
			    (server = this.Client.Servers[channel.Guild.Id]) == null ||
			    !(message.Author is SocketGuildUser user) ||
			    !server.Config.ExpEnabled )
				return;

			LastMessageId = message.Id;

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

			UserData userData = dbContext.GetOrAddUser(server.Id, user.Id);

			if( !string.IsNullOrEmpty(message.Content) )
				userData.CountMessages++;
			if( message.Attachments.Any() )
				userData.CountAttachments++;

			userData.ExpRelative = server.Config.ExpPerMessage * userData.CountMessages +
								   server.Config.ExpPerAttachment * userData.CountAttachments;

			// Recalculate level and assign appropriate roles if it changed.
			try
			{
				double exp = userData.ExpRelative;
				double b = server.Config.BaseExpToLevelup;
				//lvl = ( sqrt(4exp + base)/sqrt(base) - 1)/2
				//exp = base * lvl * (lvl + 1)
				Int64 newLvl = (Int64) ((Math.Sqrt(4 * exp + b) / Math.Sqrt(b) - 1) / 2);
				if( newLvl != userData.Level )
				{
					if( newLvl > userData.Level )
					{
						userData.KarmaCount += server.Config.KarmaPerLevel * (newLvl - userData.Level);
					}

					bool IsRoleToAssign(RoleConfig r)
					{
						return r.ExpLevel != 0 && ((server.Config.ExpCumulativeRoles && r.ExpLevel <= newLvl) || (!server.Config.ExpCumulativeRoles && r.ExpLevel == newLvl));
					}

					IEnumerable<SocketRole> rolesToAssign = server.Roles.Values.Where(IsRoleToAssign).Select(r => server.Guild.GetRole(r.RoleId));
					if( rolesToAssign.Any() )
						await user.AddRolesAsync(rolesToAssign);

					bool IsRoleToRemove(RoleConfig r)
					{
						return r.ExpLevel != 0 &&
						       ((server.Config.ExpCumulativeRoles && r.ExpLevel > newLvl) ||
						       (!server.Config.ExpCumulativeRoles && r.ExpLevel != newLvl));
					}

					IEnumerable<SocketRole> rolesToRemove = server.Roles.Values.Where(IsRoleToRemove).Select(r => server.Guild.GetRole(r.RoleId)).Where(r => r != null);
					foreach( SocketRole roleToRemove in rolesToRemove )
						await user.RemoveRoleAsync(roleToRemove);

					if( newLvl > userData.Level && server.Config.ExpAnnounceLevelup )
					{
						SocketRole role = server.Roles.Values.Where(r => r.ExpLevel == newLvl)
							.Select(r => server.Guild.GetRole(r.RoleId)).FirstOrDefault();
						await this.Client.SendMessageToChannel(channel, string.Format(LevelupString, user.Id, role?.Name ?? newLvl.ToString()));
					}

					userData.Level = newLvl;
				}
			}
			catch(Exception e)
			{
				if( !this.ServersWithException.Contains(server.Id) )
				{
					this.ServersWithException.Add(server.Id);
					await channel.SendMessageAsync("My configuration on this server is bork, please advise the Admins to fix it :<");
				}
				await this.HandleException(e, "Levelup error", server.Id);
			}

			dbContext.SaveChanges();
			dbContext.Dispose();
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
