using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Botwinder.Entities;
using Discord;

using guid = System.UInt64;

namespace Botwinder.Bot
{
	public partial class Commands
	{
		public static void HexToRgb(string hex, out byte r, out byte g, out byte b)
		{
			int color = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
			r = (byte)((color & 0xFF0000) >> 16);
			g = (byte)((color & 0xFF00) >> 8);
			b = (byte)(color & 0xFF);
		}


		public static List<Command> GetAdminCommands<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new()
		{
			List<Command> commands = new List<Command>();

// !channels
			Command newCommand = new Command("channels");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "List of Channels on this server, and their IDs";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) =>{
				string response = "_This command is obsolete, use DevMode <http://botwinder.info/img/devMode.png>_\n\nList of Channels on this server:";
				foreach(Channel channel in e.Message.Server.AllChannels)
				{
					string newString = "\n "+ (channel.Name == "@everyone" ? "@-everyone" : channel.Name) +" | "+ channel.Id.ToString();
					if( response.Length + newString.Length >= GlobalConfig.MessageCharacterLimit )
					{
						await e.Message.Channel.SendMessageSafe(response);
						response = "";
					}

					response += newString;
				}

				await e.Message.Channel.SendMessageSafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("getChannels"));
			commands.Add(newCommand.CreateAlias("getchannels"));

			// !roles
			newCommand = new Command("roles");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "List of Roles on this server, and their IDs";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) =>{
				string response = string.Format("_This command is obsolete, use `{0}getRole name`_\n\nList of Roles on this server:", e.Server.ServerConfig.CommandCharacter);
				foreach(Role role in e.Message.Server.Roles)
				{
					string newString = "\n "+ (role.Name == "@everyone" ? "@-everyone" : role.Name) +" | "+ role.Id.ToString() +" | "+ role.Color.ToString();
					if( response.Length + newString.Length >= GlobalConfig.MessageCharacterLimit )
					{
						await e.Message.Channel.SendMessageSafe(response);
						response = "";
					}

					response += newString;
				}

				await e.Message.Channel.SendMessageSafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("getRoles"));
			commands.Add(newCommand.CreateAlias("getroles"));

			// !getRole
			newCommand = new Command("getRole");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Get a name, id and color of `roleID` or `roleName` parameter.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) =>{
				guid id = 0;
				Role roleFromId = null;
				List<Role> roles = null;
				if( string.IsNullOrEmpty(e.TrimmedMessage) || (!guid.TryParse(e.TrimmedMessage, out id) && (roles = e.Message.Server.Roles.Where(r => r.Name.ToLower().Contains(e.TrimmedMessage.ToLower())).ToList()).Count() == 0) )
				{
					await e.Message.Channel.SendMessageSafe("Role not found.");
					return;
				}
				else if( id != 0 && (roleFromId = e.Message.Server.GetRole(id)) != null )
				{
					roles = new List<Role>();
					roles.Add(roleFromId);
				}

				if( roles == null || !roles.Any() )
				{
					await e.Message.Channel.SendMessageSafe("Role not found.");
					return;
				}

				foreach(Role role in roles)
				{
					string hex = BitConverter.ToString(new byte[]{role.Color.R, role.Color.G, role.Color.B}).Replace("-", "");
					await e.Message.Channel.SendMessageSafe(string.Format("Role: `{0}`\n  Id: `{1}`\n  Position: `{2}`\n  Color: `rgb({3},{4},{5})` | `hex(#{6})`", role.Name, role.Id, role.Position, role.Color.R, role.Color.G, role.Color.B, hex));
				}
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("getrole"));

// !createRole
			newCommand = new Command("createRole");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Create a new role with empty permissions. Use with parameter `name`.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) =>{
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.Message.Channel.SendMessageSafe("What name should the new role have?");
					return;
				}

				string result = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
				try
				{
					Role role = await e.Message.Server.CreateRole(e.TrimmedMessage, ServerPermissions.None);
					result = string.Format("Role `{0}` was created with ID: `{1}`", role.Name, role.Id);
				} catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null )
					{
						if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
							result = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
						else
							result = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "Discord returned to me unknown error. _\\*shrugs*_";
					}
					else
					{
						client.LogException(exception, e);
					}
				}
				await e.Message.Channel.SendMessageSafe(result);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("createrole"));

// !setcolor
			newCommand = new Command("setColor");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Set a new color for the Role. Params: `roleID hexColor`";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) =>{
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					await e.Message.Channel.SendMessageSafe("Invalid arguments.");
					return;
				}

				guid id;
				Role foundRole = null;
				if( !guid.TryParse(e.MessageArgs[0], out id) || (foundRole = e.Message.Server.GetRole(id)) == null )
				{
					await e.Message.Channel.SendMessageSafe("Invalid Role ID");
					return;
				}

				string response = "Done!";
				try{
					byte r,g,b;
					string hex = (e.MessageArgs[1].StartsWith("#") ? e.MessageArgs[1].Substring(1) : e.MessageArgs[1]);
					HexToRgb(hex, out r, out g, out b);
					await foundRole.Edit( color: new Color(r, g, b) );
#pragma warning disable 0168
				} catch(Exception exception)
				{
					response = "Invalid Color (or I don't have permissions to edit that role, or the role doesn't exist...)";
				}
#pragma warning restore 0168

				await e.Message.Channel.SendMessageSafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("setcolor"));

// !shoo
			newCommand = new Command("shoo");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Botwinder will leave this server.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner;
			newCommand.OnExecute += async (sender, e) =>{
				await e.Message.Channel.SendMessageSafe("You know where to find me! <http://botwinder.info>\n_\\*frameshifts out of the chat*_\n~");
				await Task.Delay(TimeSpan.FromSeconds(2f));
				await Task.Delay(TimeSpan.FromSeconds(3f));
				await e.Message.Server.Leave();
			};
			commands.Add(newCommand);


// !hideChannel
			newCommand = new Command("hideChannel");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Hide or un-hide the current channel by denying or allowing the Read Messages permission for everyone. (Use with _\"silent\"_ parameter for silent execution.)";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) => {
				if( !e.Message.Server.CurrentUser.ServerPermissions.ManageChannels && !e.Message.Server.CurrentUser.ServerPermissions.Administrator )
				{
					await e.Message.Channel.SendMessageSafe("I don't have `ManageChannels` permission >_<");
					return;
				}
				if( e.Message.Server.Id == e.Message.Channel.Id )
				{
					await e.Message.Channel.SendMessageSafe("I can not hide the default channel.");
					return;
				}

				Role role = e.Message.Server.GetRole(e.Message.Server.Id);
				await Task.Delay(TimeSpan.FromSeconds(1f));
				ChannelPermissionOverrides originalPermissions = e.Message.Channel.GetPermissionsRule(role);
				await e.Message.Channel.AddPermissionsRule(role, new ChannelPermissionOverrides(basePerms: originalPermissions, readMessages: originalPermissions.ReadMessages == PermValue.Deny ? PermValue.Inherit : PermValue.Deny));
				if( !string.IsNullOrEmpty(e.TrimmedMessage) && (e.TrimmedMessage == "delete" || e.TrimmedMessage == "silent") )
					await e.Message.Delete();
				else
					await e.Message.Channel.SendMessageSafe(originalPermissions.ReadMessages == PermValue.Deny ? "This channel is now visible." : "Channel hidden.");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("hidechannel"));

// !archive
			newCommand = new Command("archive");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "This will pull the **whole** channel history and save it as a text file. Use as `archive nice` to use nice formatting. There is a limit of 50 000 messages for safety reasons, but you can poke Rhea, she can archive without limits. (..because this operation should be observed, as it is really intense on memory.) <http://botwinder.info/img/archive.png>";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) =>{
				Operation op = null;
				try
				{
					Message msg = await e.Message.Channel.SendMessage("This may take a while, but it's okay! =]");

					if( !Directory.Exists("archives") )
						Directory.CreateDirectory("archives");
					string path;
					for( int i = 0; true; i++ )
					{
						path = Path.Combine("archives", string.Format("archive-{0}_{1:00}.log", e.Message.Channel.Name, i));
						if( !File.Exists(path) )
							break;
					}

					Log archive = new Log(path);
					guid lastMessageID = e.Message.Id;
					Message[] chunk = null;
					List<Message> messages = new List<Message>();
					bool huge = e.Server.IsGlobalAdmin(e.Message.User);

					op = Operation.Create<TUser>(client, e, true);
					if( await op.Await(client, async () => await e.Message.Channel.SendMessageSafe(string.Format(GlobalConfig.OperationQueuedText, client.CurrentOperations.Count, e.Command.ID))) )
						return;
					op.CurrentState = Operation.State.Running;

					while( (chunk = await e.Message.Channel.DownloadMessages(relativeMessageId: lastMessageID)) != null && chunk.Length > 0 && (messages.Count < GlobalConfig.ArchiveMessageLimit || huge) )
					{
						if( op != null && await op.AwaitConnection(client) )
						{
							await msg.Edit("This operation was canceled.");
							return;
						}

						messages.AddRange(chunk);
						lastMessageID = chunk[chunk.Length - 1].Id;

						await Task.Yield(); //Take a break, do other things!
					}

					await archive.ArchiveList(messages, e.TrimmedMessage.Contains("nice"));
					if( messages.Count < GlobalConfig.ArchiveMessageLimit || huge )
						await e.Message.Channel.SendMessageSafe("Whew! I saved " + messages.Count + " messages.");
					else
						await e.Message.Channel.SendMessageSafe("I couldn't get all of them, the channel is a bit too..**HUGE**! Anyway I saved the last " + messages.Count + " messages at least.\nIf you would like me to save more, please get in touch with Rhea... I'm scared of big files, I want her to watch over me >_<");

					if( e.Message.Server.GetUser(e.Message.Client.CurrentUser.Id).ServerPermissions.AttachFiles && (new FileInfo(path)).Length < 8000000 )
					{
						Task<Message> task = e.Message.Channel.SendFile(path);
						if( await Task.WhenAny(task, Task.Delay(GlobalConfig.FileUploadTimeout)) == task )
						{
							await task;
						}
						else
						{
							await e.Message.Channel.SendMessageSafe("The file seems to be too large to be able to upload it, you can poke `Rhea#0321` and she can send it to you :sunglasses:");
						}
					}
					else
						await e.Message.Channel.SendMessageSafe("The file seems to be too large to be able to upload it (or I don't have permissions,) you can poke `Rhea#0321` and she can send it to you :sunglasses:");
				} catch( Exception exception ) when( exception.GetType() != typeof(Discord.Net.HttpException) )
				{
					client.LogException(exception, e);
				}

				if( op != null )
					op.Finalise(client);
				op = null;
			};
			commands.Add(newCommand);

// !promoteEveryone
			newCommand = new Command("promoteEveryone");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Assign a role to everyone on your server. Please ensure correct hierarchy before using this command. Use with `roleID` parameter, this does not accept name of the role.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) => {
				Operation op = null;
				try
				{
					guid id = 0;
					Role roleFromId = null;
					if( string.IsNullOrEmpty(e.TrimmedMessage) || !guid.TryParse(e.TrimmedMessage, out id) || (roleFromId = e.Message.Server.GetRole(id)) == null )
					{
						await e.Message.Channel.SendMessageSafe("Role not found.");
						return;
					}

					Message msg = await e.Message.Channel.SendMessage("Working on it!");
					int count = 0;
					int fails = 0;
					op = Operation.Create<TUser>(client, e, true);
					if( await op.Await(client, async () => await e.Message.Channel.SendMessageSafe(string.Format(GlobalConfig.OperationQueuedText, client.CurrentOperations.Count, e.Command.ID))) )
						return;
					op.CurrentState = Operation.State.Running;

					foreach( User user in e.Message.Server.Users )
					{
						if( op != null && await op.AwaitConnection(client) )
						{
							await msg.Edit("This operation was canceled.");
							return;
						}

						if( !user.HasRole(roleFromId) )
						{
							try
							{
								count++;
								await user.AddRoles(roleFromId);
							}
							catch( Discord.Net.HttpException ex )
							{
								if( ex.StatusCode != System.Net.HttpStatusCode.Forbidden )
									throw;

								fails++;
							}
						}
					}
					await e.Message.Channel.SendMessageSafe(string.Format("Done!\n{0} successful;\n{1} failed! (It will fail to assign the role according to the role hierarchy.)", count, fails));
				} catch( Exception exception ) when( exception.GetType() != typeof(Discord.Net.HttpException) )
				{
					client.LogException(exception, e);
				}

				if( op != null )
					op.Finalise(client);
				op = null;
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("promoteeveryone"));

			return commands;
		}
	}
}
