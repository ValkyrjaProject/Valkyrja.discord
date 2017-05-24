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
		public static List<Command> GetPublicCommands<TUser>(IBotwinderClient<TUser> client) where TUser : UserData, new()
		{
			List<Command> commands = new List<Command>();

// !publicRoles
			Command newCommand = new Command("publicRoles");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "See what Public Roles can you join on this server.";
			newCommand.OnExecute += async (sender, e) =>{
				string response = string.Format("You can use `{0}join` and `{0}leave` commands with these parameters: ", (e.Server as Server<TUser>).ServerConfig.CommandCharacter);

				guid[] publicRoles = (e.Server as Server<TUser>).ServerConfig.PublicRoleIDs;
				if( publicRoles == null || publicRoles.Length == 0 )
				{
					response = "I'm sorry, but there are no public roles on this server.";
				}
				else
				{
					for(int i = 0; i < publicRoles.Length; i++)
					{
						string newString;
						Role role = e.Message.Server.GetRole(publicRoles[i]);
						if( role != null )
						{
							newString = (i == 0 ? "`" : i == publicRoles.Length -1 ? " and `" : ", `") + role.Name +"`";
							if( newString.Length + response.Length > GlobalConfig.MessageCharacterLimit )
							{
								await e.Message.Channel.SendMessageSafe(response);
								response = "";
							}
							response += newString;
						}
					}
				}

				await e.Message.Channel.SendMessageSafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("publicroles"));
			commands.Add(newCommand.CreateAlias("PublicRoles"));

// !join
			newCommand = new Command("join");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameter, name of a Role that you wish to join.";
			newCommand.OnExecute += async (sender, e) =>{
				string response = "";
				Role role = null;
				guid[] publicRoles = (e.Server as Server<TUser>).ServerConfig.PublicRoleIDs;
				try
				{
					if( publicRoles == null || publicRoles.Length == 0 )
						response = "I'm sorry, but there are no public roles on this server.";
					else
						response = await (e.Server as Server<TUser>).AssignRole(e.Message.User, e.TrimmedMessage, r => publicRoles.Contains((role = r).Id));
				} catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null )
					{
						if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
							response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
						else
							response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
					}
					else
					{
						client.LogException(exception, e);
						response = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
					}
				}

				if( string.IsNullOrWhiteSpace(response) )
				{
					response = "Done!";

					Channel logChannel = null;
					if( e.Server.ServerConfig.ModChannelLogMembers && (logChannel = e.Message.Server.GetChannel(e.Server.ServerConfig.ModChannel)) != null )
					{
						string message = string.Format("`{0}`: __{1}__ joined _{2}_.", Utils.GetTimestamp(), e.Message.User.Name, (role == null ? e.TrimmedMessage : role.Name));
						await logChannel.SendMessageSafe(message);
					}
				}

				Message msg = await e.Message.Channel.SendMessage(response);
				if( e.Server.ServerConfig.RemoveJoin )
				{
					await Task.Delay(TimeSpan.FromSeconds(10f));
					await e.Message.Delete();
					await Task.Delay(TimeSpan.FromSeconds(1f));
					await msg.Delete();
				}
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("Join"));

// !leave
			newCommand = new Command("leave");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Use with parameter, name of a Role that you wish to leave.";
			newCommand.OnExecute += async (sender, e) =>{
				Role role = null;
				string response = "";
				guid[] publicRoles = (e.Server as Server<TUser>).ServerConfig.PublicRoleIDs;
				try
				{
					if( publicRoles == null || publicRoles.Length == 0 )
						response = "I'm sorry, but there are no public roles on this server.";
					else
						response = await (e.Server as Server<TUser>).RemoveRole(e.Message.User, e.TrimmedMessage, r => publicRoles.Contains((role = r).Id));
				} catch(Exception exception)
				{
					Discord.Net.HttpException ex = exception as Discord.Net.HttpException;
					if( ex != null )
					{
						if( ex.StatusCode == System.Net.HttpStatusCode.Forbidden )
							response = "Something went wrong, I may not have server permissions to do that.\n(Hint: <http://i.imgur.com/T8MPvME.png>)";
						else
							response = GlobalConfig.DisplayError500 ? "Discord server may have had a derp there, please try again if it didn't work!" : "";
					}
					else
					{
						client.LogException(exception, e);
						response = "Unknown error, please poke <@"+ (client.GlobalConfig.OwnerIDs != null && client.GlobalConfig.OwnerIDs.Length > 0 ? client.GlobalConfig.OwnerIDs[0] : GlobalConfig.Rhea) +"> (`"+ GlobalConfig.RheaName +"`) to take a look x_x";
					}
				}

				if( string.IsNullOrWhiteSpace(response) )
				{
					response = "Done!";

					Channel logChannel = null;
					if( e.Server.ServerConfig.ModChannelLogMembers && (logChannel = e.Message.Server.GetChannel(e.Server.ServerConfig.ModChannel)) != null )
					{
						string message = string.Format("`{0}`: __{1}__ left _{2}_.", Utils.GetTimestamp(), e.Message.User.Name, (role == null ? e.TrimmedMessage : role.Name));
						await logChannel.SendMessageSafe(message);
					}
				}

				Message msg = await e.Message.Channel.SendMessage(response);
				if( e.Server.ServerConfig.RemoveJoin )
				{
					await Task.Delay(TimeSpan.FromSeconds(10f));
					await e.Message.Delete();
					await Task.Delay(TimeSpan.FromSeconds(1f));
					await msg.Delete();
				}
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("Leave"));

// !dice
			newCommand = new Command("dice");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Roll a dice! Use one of the following formats: `6` / `3 d20` / `15 d6 >5` _(Yes, with spaces!)_ where the `d` specifies how many sides the dice has, and the `>` filters results and total count to be greater than five in the example. (You can also use `<` or `=`)";
			newCommand.OnExecute += async (sender, e) =>{
				if( e.MessageArgs == null || e.MessageArgs.Length == 0 )
				{
					await e.Message.Channel.SendMessageSafe("You forgot something!");
					return;
				}

				int dice = 0;
				int quantity = 1;
				int equality = 0;
				bool gt, lt, eq;
				gt = lt = eq = false;
				string quantityString = null;
				string diceString = e.MessageArgs[0];
				string equalityString = null;
				if( e.MessageArgs.Length >= 2 )
				{
					quantityString = e.MessageArgs[0];
					diceString = e.MessageArgs[1];
				}
				if( e.MessageArgs.Length == 3 )
				{
					equalityString = e.MessageArgs[2];
					gt = equalityString.StartsWith(">");
					lt = equalityString.StartsWith("<");
					eq = equalityString.StartsWith("=");
				}

				if( !int.TryParse(diceString.TrimStart('d'), out dice) || dice <= 0 ||
				    (!string.IsNullOrEmpty(quantityString) && !int.TryParse(quantityString, out quantity)) || quantity <= 0 ||
				    (e.MessageArgs.Length == 3 && ((!gt && !lt && !eq) || !int.TryParse(equalityString.TrimStart('>', '<', '='), out equality) || equality < 1 || equality > dice)) )
				{
					await e.Message.Channel.SendMessageSafe("Use one of the following formats: `6` / `3 d20` / `15 d6 >5` _(With spaces.)_");
					return;
				}

				if( quantity > 50 )
				{
					await e.Message.Channel.SendMessageSafe("Can you really fit all of those in your hand? o_O");
					return;
				}

				if( dice*quantity > 10000 )
				{
					await e.Message.Channel.SendMessageSafe("Could you try throwing a smaller dice please? This is too heavy.");
					return;
				}

				string msg = "";
				int total = 0;
				int count = 0;
				for(int i = 0; i < quantity; i++)
				{
					int roll = Utils.Random.Next(1, dice +1);
					if( e.MessageArgs.Length < 3 )
						total += roll;
					if( (gt && roll > equality) ||
					    (lt && roll < equality) ||
					    (eq && roll == equality) )
					{
						total += roll;
						count++;
					}

					msg += ((i != 0 && i < quantity-1) ? ", " : (i == quantity-1 ? " and " : "")) + roll.ToString();
				}

				if( quantity == 1 )
					await e.Message.Channel.SendMessageSafe(string.Format("<@{0}> rolled {1}.", e.Message.User.Id, total));
				else if( e.MessageArgs.Length == 3 )
					await e.Message.Channel.SendMessageSafe(string.Format("<@{0}> rolled {1}. (Sum of {2} rolls `{3}`: {4})", e.Message.User.Id, msg, count, equalityString, total));
				else
					await e.Message.Channel.SendMessageSafe(string.Format("<@{0}> rolled {1}. (Sum: {2})", e.Message.User.Id, msg, total));
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("roll"));

// !ping
			newCommand = new Command("ping");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "A what?";
			newCommand.OnExecute += async (sender, e) =>{
				await client.Ping(e.Message, e.IsPm ? null : e.Server as Server<TUser>);
			};
			commands.Add(newCommand);

// !wat
			newCommand = new Command("wat");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "The best command of all time.";
			newCommand.OnExecute += async (sender, e) =>{
				await e.Message.Channel.SendMessageSafe("**-wat-**\n<http://destroyallsoftware.com/talks/wat>");
			};
			commands.Add(newCommand);

// !guide
			newCommand = new Command("guide");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "Need help setting up Botwinder?";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin;
			newCommand.OnExecute += async (sender, e) => {
				await e.Message.Channel.SendMessageSafe("**Need help setting up Botwinder?** Comprehensive configuration guide here!\n<https://www.youtube.com/watch?v=BUbMd4dSsE0&list=PLq3HkeraP8n27r0_r2hFRWK_xvcQXqQaI&index=3>");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("guides"));

// !discordGuide
			newCommand = new Command("discordGuide");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "New to Discord? Need some help with it? Read a **guide**! <https://www.discordguide.us>";
			newCommand.OnExecute += async (sender, e) => {
				await e.Message.Channel.SendMessageSafe("**New to Discord?** Need some help with it? Read a **guide**!\n<https://www.discordguide.us>\nhttps://discord.gg/D6g6wSm\n\n" +
				                                    "Need help setting up your server and **permissions**? This is how to correctly do just that!\n<http://rhea-ayase.eu/articles/2016-12/Discord-Guide-Server-setup-and-permissions>");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("discordguide"));
			commands.Add(newCommand.CreateAlias("discordHelp"));
			commands.Add(newCommand.CreateAlias("discordhelp"));

// !discordPermissions
			newCommand = new Command("discordPermissions");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "Need help setting up your server and **permissions**? Take a look at this guide: <http://rhea-ayase.eu/articles/2016-12/Discord-Guide-Server-setup-and-permissions>";
			newCommand.OnExecute += async (sender, e) => {
				await e.Message.Channel.SendMessageSafe("Need help setting up your server and **permissions**? This is how to correctly do just that!\n<http://rhea-ayase.eu/articles/2016-12/Discord-Guide-Server-setup-and-permissions>");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("discordpermissions"));
			commands.Add(newCommand.CreateAlias("permissionshelp"));
			commands.Add(newCommand.CreateAlias("permissionsHelp"));

// !bunneh
			newCommand = new Command("bunneh");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "Get a bunneh picture, because Rhea loves them.";
			newCommand.IsHidden = true;
			newCommand.OnExecute += async (sender, e) => {
				if( Directory.Exists(GlobalConfig.DataFolder) && Directory.Exists(Path.Combine(GlobalConfig.DataFolder, GlobalConfig.BunnehDataFolder)) )
				{
					Regex validExtensions = new Regex(".*(jpg|png|gif).*");
					DirectoryInfo folder = new DirectoryInfo(Path.Combine(GlobalConfig.DataFolder, GlobalConfig.BunnehDataFolder));
					FileInfo[] files = folder.GetFiles();
					for( int i = 0; files != null && i < 5; i++ )
					{
						int index = Utils.Random.Next(0, files.Length);
						if( validExtensions.Match(files[index].Extension).Success )
						{
							await e.Message.Channel.SendFile(files[index].FullName);
							break;
						}
					}
				}
			};
			commands.Add(newCommand);

			return commands;
		}
	}
}
