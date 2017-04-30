using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Botwinder.Entities;
using Discord;
using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class Giveaways: JsonModule<Giveaways.ServerList>
	{
		public class Server
		{
			public guid ID;
			public guid[] Users;
		}
		public class ServerList
		{
			public Server[] Servers;
		}

		protected override string Filename => "giveaways.json";
		private DateTime LastChanged{ get; set; }
		private DateTime LastSaved{ get; set; }


		private ConcurrentDictionary<guid, ConcurrentDictionary<guid, Discord.User>> ServerDictionary{ get; set; }
		private ConcurrentDictionary<guid, ConcurrentDictionary<guid, Discord.User>> ClosedGiveaways{ get; set; }
		private ConcurrentDictionary<guid, guid> RestrictedGiveaways = new ConcurrentDictionary<guid, guid>();

		private int Count{ get{ return this.ServerDictionary.Count; } }
		private bool ContainsKey(guid id) => this.ServerDictionary.ContainsKey(id);

		private ConcurrentDictionary<guid, Discord.User> this[guid id]
		{
			get{
				if( !this.ServerDictionary.ContainsKey(id) )
					return null;
				return this.ServerDictionary[id];
			}
			set{
				if( value == null && this.ServerDictionary.ContainsKey(id) )
					this.ServerDictionary.Remove(id);
				else if( value != null )
				{
					if( this.ServerDictionary.ContainsKey(id) )
						this.ServerDictionary[id] = value;
					else
						this.ServerDictionary.Add(id, value);
				}
			}
		}


		public override List<Command> Init<TUser>(IBotwinderClient<TUser> client)
		{
			List<Command> commands;
			Command newCommand;

			if( !client.GlobalConfig.GiveawaysEnabled )
			{
				commands = new List<Command>();
				newCommand = new Command("g");
				newCommand.Type = Command.CommandType.ChatOnly;
				newCommand.Description = "Participate in currently active giveaway.";
				newCommand.OnExecute += async (sender, e) => {
					await e.Message.Channel.SendMessageSafe("Giveaways are currently disabled for technical difficulties. Please be patient, we are working on it.");
				};
				commands.Add(newCommand);
				newCommand = newCommand.CreateCopy("giveaway");
				newCommand.Description = "Manage a giveaway on your server with parameters start, end, or roll. Use the command without parameters for more details.";
				newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
				commands.Add(newCommand);
				return commands;
			}

			commands = base.Init<TUser>(client);

			this.ServerDictionary = new ConcurrentDictionary<guid, ConcurrentDictionary<guid, Discord.User>>();
			this.ClosedGiveaways = new ConcurrentDictionary<guid, ConcurrentDictionary<guid, Discord.User>>();

			if( this.Data != null && this.Data.Servers != null )
			{
				foreach(Server serverData in this.Data.Servers)
				{
					Server<TUser> server = null;
					if( !client.Servers.ContainsKey(serverData.ID) || (server = client.Servers[serverData.ID]) == null )
						continue;

					ConcurrentDictionary<guid, Discord.User> dict = new ConcurrentDictionary<guid, Discord.User>();
					for(int i = 0; i < serverData.Users.Length; i++)
					{
						Discord.User user = server.DiscordServer.GetUser(serverData.Users[i]);
						if( user != null )
							dict.Add(user.Id, user);
					}

					this.ServerDictionary.Add(serverData.ID, dict);
				}
			}

// !giveaway
			newCommand = new Command("giveaway");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Manage a giveaway on your server with parameters start, end, or roll. Use the command without parameters for more details.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				string invalidParameters = string.Format("Invalid parameters. Usage:\n" +
				                                         "    `{0}{1} start/end` - Start or end a giveaway (you can't re-open it so think twice before closing)\n" +
				                                         "    `{0}{1} start roleID` - You can limit the giveaway only to people with the optional roleID (or roleMention) parameter. (Use `{0}getRole` to get the ID.)\n" +
				                                         "    `{0}{1} roll` - Pick a winner at random (you have to end the giveaway first)\n" +
				                                         "    `{0}g` - Participate in currently active giveaway.",
					e.Server.ServerConfig.CommandCharacter, e.Command.ID
				);

				string responseMessage = "I haz an error! <@"+ client.GlobalConfig.OwnerIDs[0] +"> save me!";

				if( e.MessageArgs == null || e.MessageArgs.Length < 1 )
				{
					responseMessage = invalidParameters;
				}
				else
				{
					try
					{
						switch(e.MessageArgs[0])
						{
						case "new":
						case "open":
						case "start":
						case "begin":
							if( ContainsKey(e.Server.ID) )
							{
								responseMessage = string.Format("There is a giveaway running! Close it using `{0}{1} end` before starting a new one.", e.Server.ServerConfig.CommandCharacter, e.Command.ID);
								break;
							}

							this[e.Server.ID] = new ConcurrentDictionary<guid, Discord.User>();
							responseMessage = "New giveaway is open, you can participate by typing `" + e.Server.ServerConfig.CommandCharacter.ToString() + "g` in the chat.";

							guid id;
							Role role = null;
							if( e.MessageArgs.Length > 1 && (guid.TryParse(e.MessageArgs[1], out id) || guid.TryParse(e.MessageArgs[1].Trim('<','@','&','>'), out id)) && (role = e.Server.DiscordServer.GetRole(id)) != null )
							{
								if( this.RestrictedGiveaways.ContainsKey(e.Server.ID) )
									this.RestrictedGiveaways[e.Server.ID] = id;
								else
									this.RestrictedGiveaways.Add(e.Server.ID, id);

								responseMessage += "\n_(Only members of the `"+ role.Name +"` role can participate.)_";
							}

							this.LastChanged = DateTime.UtcNow;
							break;
						case "close":
						case "stop":
						case "end":
							if( !ContainsKey(e.Server.ID) )
							{
								responseMessage = "There is nothing to " + e.MessageArgs[0] + ".";
								break;
							}

							if( this.ClosedGiveaways.ContainsKey(e.Server.ID) )
								this.ClosedGiveaways[e.Server.ID] = this[e.Server.ID];
							else
								this.ClosedGiveaways.Add(e.Server.ID, this[e.Server.ID]);

							this[e.Server.ID] = null;
							responseMessage = string.Format("The giveaway was closed with {0} participants - you can still `roll` winners.", this.ClosedGiveaways[e.Server.ID].Count);
							this.LastChanged = DateTime.UtcNow;
							goto case "roll";
						case "roll":
						case "reroll":
						case "winner":
							if( ContainsKey(e.Server.ID) || !this.ClosedGiveaways.ContainsKey(e.Server.ID) || this.ClosedGiveaways[e.Server.ID].Count == 0 )
							{
								responseMessage = "Can't roll a winner until you run and **end** the giveaway with someone actually participating in it.";
								break;
							}

							int index = Utils.Random.Next(0, this.ClosedGiveaways[e.Server.ID].Values.Count);
							Discord.User winner = this.ClosedGiveaways[e.Server.ID].Values.ElementAt(index);
							responseMessage = string.Format("<@{0}> is teh winner!", winner.Id);
							break;
						default:
							responseMessage = invalidParameters;
							break;
						}
					} catch(Exception exception)
					{
						responseMessage = "I haz an error! <@"+ client.GlobalConfig.OwnerIDs[0] +"> save me!";
						responseMessage += "\n"+ exception.Message;
					}
				}

				await e.Message.Channel.SendMessageSafe(responseMessage);
			};
			commands.Add(newCommand);

// !g
			newCommand = new Command("g");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Participate in currently active giveaway.";
			newCommand.OnExecute += async (sender, e) =>{
				if( !ContainsKey(e.Server.ID) )
				{
					await e.Message.Channel.SendMessageSafe("There ain't no giveaway runnin!");
					return;
				}

				Role role = null;
				if( this.RestrictedGiveaways.ContainsKey(e.Server.ID) &&
				    (role = e.Server.DiscordServer.GetRole(this.RestrictedGiveaways[e.Server.ID])) != null &&
				    e.Message.User.Roles.FirstOrDefault(r => r.Id == this.RestrictedGiveaways[e.Server.ID]) == null )
				{
					await e.Message.Channel.SendMessageSafe("This giveaway is restricted only to people with the `"+ role.Name +"` role.");
					return;
				}

				if( !this[e.Server.ID].ContainsKey(e.Message.User.Id) )
				{
					this[e.Server.ID].Add(e.Message.User.Id, e.Message.User);
					await e.Message.Channel.SendMessageSafe(string.Format("<@{0}> jumped on the train!", e.Message.User.Id));
					this.LastChanged = DateTime.UtcNow;
				}
			};
			commands.Add(newCommand);

			return commands;
		}

#pragma warning disable 1998
		public override async Task Update<TUser>(IBotwinderClient<TUser> client)
#pragma warning restore 1998
		{
			if( !client.GlobalConfig.GiveawaysEnabled )
				return;

			if( this.LastSaved < this.LastChanged )
			{
				this.LastSaved = DateTime.UtcNow;
				SaveAsync();
			}
		}

		protected override void Save()
		{
			this.Data.Servers = new Server[this.ServerDictionary.Count];
			for(int i = 0; i < this.Data.Servers.Length; i++)
			{
				this.Data.Servers[i] = new Server();
				this.Data.Servers[i].ID = this.ServerDictionary.ElementAt(i).Key;
				this.Data.Servers[i].Users = this.ServerDictionary.Values.ElementAt(i).Values.Select(u => u.Id).ToArray();
			}

			base.Save();
		}
	}
}
