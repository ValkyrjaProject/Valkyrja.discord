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
	public class ExtraFeatures: IModule
	{
		private const string ErrorPermissionsString = "I don't have necessary permissions.";
		private const string ErrorRoleNotFound = "I did not find a role based on that expression.";
		private const string ErrorTooManyFound = "I found more than one role with that expression, please be more specific.";
		private const string ErrorUnknownString = "Unknown error, please poke <@{0}> to take a look x_x";
		private const string TempChannelConfirmString = "Here you go! <3\n_(Temporary channel `{0}` was created.)_";
		//private readonly Regex EmbedParamRegex = new Regex("--?\\w+\\s(?!--?\\w|$).*?(?=\\s--?\\w|$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex EmbedParamRegex = new Regex("--?\\w+.*?(?=\\s--?\\w|$)", RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex EmbedOptionRegex = new Regex("--?\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

		private ValkyrjaClient Client;

		private readonly TimeSpan TempChannelDelay = TimeSpan.FromMinutes(3);


		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

// !tempChannel
			Command newCommand = new Command("tempChannel");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Creates a temporary voice channel. This channel will be destroyed when it becomes empty, with grace period of three minutes since it's creation.";
			newCommand.ManPage = new ManPage("[userLimit] <channelName>", "`[userLimit]` - Optional user limit.\n\n`<channelName>` - Name of the new temporary voice channel.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
			newCommand.OnExecute += async e => {
				if( e.Server.Config.TempChannelCategoryId == 0 )
				{
					await e.SendReplySafe("This command has to be configured on the config page (\"other\" section) <https://valkyrja.app/config>");
					return;
				}

				if( string.IsNullOrWhiteSpace(e.TrimmedMessage) )
				{
					await e.SendReplySafe($"Usage: `{e.Server.Config.CommandPrefix}tempChannel <name>` or `{e.Server.Config.CommandPrefix}tempChannel [userLimit] <name>`");
					return;
				}

				int limit = 0;
				bool limited = int.TryParse(e.MessageArgs[0], out limit);
				StringBuilder name = new StringBuilder();
				for(int i = limited ? 1 : 0; i < e.MessageArgs.Length; i++)
				{
					name.Append(e.MessageArgs[i]);
					name.Append(" ");
				}
				string responseString = string.Format(TempChannelConfirmString, name.ToString());

				try
				{
					RestVoiceChannel tempChannel = null;
					if( limited )
						tempChannel = await e.Server.Guild.CreateVoiceChannelAsync(name.ToString(), c => {
							c.CategoryId = e.Server.Config.TempChannelCategoryId;
							c.UserLimit = limit;
						});
					else
						tempChannel = await e.Server.Guild.CreateVoiceChannelAsync(name.ToString(), c => c.CategoryId = e.Server.Config.TempChannelCategoryId);

					if( e.Server.Config.TempChannelGiveAdmin )
						await tempChannel.AddPermissionOverwriteAsync(e.Message.Author, new OverwritePermissions(manageChannel: PermValue.Allow, manageRoles: PermValue.Allow, moveMembers: PermValue.Allow, muteMembers: PermValue.Allow, deafenMembers: PermValue.Allow));

					ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
					ChannelConfig channel = dbContext.Channels.AsQueryable().FirstOrDefault(c => c.ServerId == e.Server.Id && c.ChannelId == tempChannel.Id);
					if( channel == null )
					{
						channel = new ChannelConfig{
							ServerId = e.Server.Id,
							ChannelId = tempChannel.Id
						};

						dbContext.Channels.Add(channel);
					}

					channel.Temporary = true;
					dbContext.SaveChanges();
					dbContext.Dispose();
				}
				catch( HttpException exception )
				{
					await e.Server.HandleHttpException(exception, $"This happened in <#{e.Channel.Id}> when trying to create a temporary channel.");
				}
				catch( Exception exception )
				{
					await this.Client.LogException(exception, e);
					responseString = string.Format(ErrorUnknownString, this.Client.GlobalConfig.AdminUserId);
				}

				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("tmp"));
			commands.Add(newCommand.CreateAlias("tc"));

// !mentionRole
			newCommand = new Command("mentionRole");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Mention a role with a message. Use with the name of the role as the first parameter and the message will be the rest.";
			newCommand.ManPage = new ManPage("<roleName> <message text>", "`<roleName>` - Name of the role to be mentioned with a ping.\n\n`<message text>` - Text that will be said with the role mention.");
			newCommand.DeleteRequest = true;
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Guild.CurrentUser.GuildPermissions.ManageRoles )
				{
					await e.SendReplySafe(ErrorPermissionsString);
					return;
				}

				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					await e.SendReplySafe($"Usage: `{e.Server.Config.CommandPrefix}{e.CommandId} <roleName> <message text>`");
					return;
				}

				IEnumerable<SocketRole> foundRoles = null;
				if( !(foundRoles = e.Server.Guild.Roles.Where(r => r.Name == e.MessageArgs[0])).Any() &&
				    !(foundRoles = e.Server.Guild.Roles.Where(r => r.Name.ToLower() == e.MessageArgs[0].ToLower())).Any() &&
				    !(foundRoles = e.Server.Guild.Roles.Where(r => r.Name.ToLower().Contains(e.MessageArgs[0].ToLower()))).Any() )
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
				string message = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[1]));

				await role.ModifyAsync(r => r.Mentionable = true);
				await Task.Delay(100);
				await e.SendReplySafe($"{role.Mention} {message}", new AllowedMentions(AllowedMentionTypes.Users | AllowedMentionTypes.Everyone | AllowedMentionTypes.Roles));
				await Task.Delay(100);
				await role.ModifyAsync(r => r.Mentionable = false);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("announce"));

// !cheatsheet
			newCommand = new Command("cheatsheet");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Send an embed cheatsheet with various moderation commands.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				EmbedBuilder embedBuilder = new EmbedBuilder();
				embedBuilder.WithTitle("Moderation commands").WithColor(16711816).WithFields(
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}op`").WithValue("Distinguish yourself as a moderator when addressing people, and allow the use of `!mute`, `!kick` & `!ban` commands."),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}mute @user(s) duration`").WithValue("Mute mentioned user(s) for `duration` (use `m`, `h` and `d`, e.g. 1h15m. This will effectively move them to the `#chill-zone` channel."),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}kick @user(s) reason`").WithValue("Kick mentioned `@users` (or IDs) with specific `reason`."),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}ban @user(s) duration reason`").WithValue("Ban mentioned `@users` (or IDs) for `duration` (use `h` and `d`, e.g. 1d12h, or zero `0d` for permanent) with specific `reason`."),
					new EmbedFieldBuilder().WithName("`reason`").WithValue("Reason parameter of the above `kick` and `ban` commands is stored in the database as a _warning_ and also PMed to the user. Please provide proper descriptions."),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}issueWarning @user(s) message`").WithValue("The same as `addWarning`, but also PM this message to the user(s)"),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}addWarning @user(s) message`").WithValue("Add a `message` to the database, taking notes of peoples naughty actions."),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}removeWarning @user`").WithValue("Remove the last added warning from the `@user` (or ID)"),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}whois @user`").WithValue("Search for a `@user` (or ID, or name) who is present on the server."),
					new EmbedFieldBuilder().WithName($"`{e.Server.Config.CommandPrefix}find expression`").WithValue("Search for a user using more complex search through all the past nicknames, etc... This will also go through people who are not on the server anymore."),
					new EmbedFieldBuilder().WithName($"`whois/find`").WithValue("Both whois and find commands will return information about the user, when was their account created, when did they join, their past names and nicknames, and all the previous warnings and bans.")
				);

				RestUserMessage msg = await e.Channel.SendMessageAsync(embed: embedBuilder.Build());
				await msg.PinAsync();

				embedBuilder = new EmbedBuilder();
				embedBuilder.WithTitle("Moderation guidelines").WithColor(16711816).WithDescription("for implementing the [theory](http://rhea-ayase.eu/articles/2017-04/Moderation-guidelines) in real situations.\n")
					.WithFields(
						new EmbedFieldBuilder().WithName("__Talking to people__").WithValue("~"),
						new EmbedFieldBuilder().WithName("Don't use threats.").WithValue("a) **Imposed consequences** - what you can do with your power (kick, ban,...) These are direct threats, avoid them.\nb) **Natural consequences** - implied effects of members actions. These can include \"the community is growing to dislike you,\" or \"see you as racist,\" etc..."),
						new EmbedFieldBuilder().WithName("Identify what is the underlying problem.").WithValue("a) **Motivation problem** - the member is not motivated to behave in acceptable manner - is a troll or otherwise enjoys being mean to people.\nb) **Ability problem** - the member may be direct without \"filters\" and their conversation often comes off as offensive while they just state things the way they see them: http://www.mit.edu/~jcb/tact.html"),
						new EmbedFieldBuilder().WithName("Conversation should follow:").WithValue("1) **Explain** the current situation / problem.\n2) **Establish safety** - you're not trying to ban them or discourage them from participating.\n3) **Call to action** - make sure to end the conversation with an agreement about what steps will be taken towards improvement.\n"),
						new EmbedFieldBuilder().WithName("__Taking action__").WithValue("~"),
						new EmbedFieldBuilder().WithName("Always log every action").WithValue("with `warnings`, and always check every member and their history."),
						new EmbedFieldBuilder().WithName("Contents of our channels should not be disrespectful towards anyone, think about minorities.").WithValue("a) Discussion topic going wild, the use of racial/homophobic or other improper language should be pointed out with an explanation that it is not cool towards minorities within our community.\nb) A member being plain disrespectful on purpose... Mute them, see their reaction to moderation talk and act on it."),
						new EmbedFieldBuilder().WithName("Posting or even spamming inappropriate content").WithValue("should result in immediate mute and only then followed by explaining correct behavior based on all of the above points."),
						new EmbedFieldBuilder().WithName("Repeated offense").WithValue("a) 1d ban, 3d ban, 7d ban - are your options depending on how severe it is.\nb) Permanent ban should be brought up for discussion with the rest of the team."),
						new EmbedFieldBuilder().WithName("Member is disrespectful to the authority.").WithValue("If you get into conflict yourself, someone is disrespectful to you as a moderator, trolling and challenging your authority - step back and ask for help, mention `@Staff` in the mod channel, and let 3rd party deal with it.")
					);

				msg = await e.Channel.SendMessageAsync(embed: embedBuilder.Build());
				await msg.PinAsync();
			};
			commands.Add(newCommand);

// !embed
			newCommand = new Command("embed");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Build an embed. Use without arguments for help.";
			newCommand.ManPage = new ManPage("<options>", "Use any combination of:\n" +
				"`--channel    ` - Channel where to send the embed.\n" +
				"`--title      ` - Title\n" +
				"`--description` - Description\n" +
				"`--footer     ` - Footer\n" +
				"`--color      ` - #rrggbb hex color used for the embed stripe.\n" +
				"`--image      ` - URL of a Hjuge image in the bottom.\n" +
				"`--thumbnail  ` - URL of a smol image on the side.\n" +
				"`--fieldName  ` - Create a new field with specified name.\n" +
				"`--fieldValue ` - Text value of a field - has to follow a name.\n" +
				"`--fieldInline` - Use to set the field as inline.\n" +
				"Where you can repeat the field* options multiple times.");
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( string.IsNullOrEmpty(e.TrimmedMessage) || e.TrimmedMessage == "-h" || e.TrimmedMessage == "--help" )
				{
					await e.SendReplySafe("```md\nCreate an embed using the following parameters:\n" +
					                      "[ --channel     ] Channel where to send the embed.\n" +
					                      "[ --title       ] Title\n" +
					                      "[ --description ] Description\n" +
					                      "[ --footer      ] Footer\n" +
					                      "[ --color       ] #rrggbb hex color used for the embed stripe.\n" +
					                      "[ --image       ] URL of a Hjuge image in the bottom.\n" +
					                      "[ --thumbnail   ] URL of a smol image on the side.\n" +
					                      "[ --fieldName   ] Create a new field with specified name.\n" +
					                      "[ --fieldValue  ] Text value of a field - has to follow a name.\n" +
					                      "[ --fieldInline ] Use to set the field as inline.\n" +
					                      "Where you can repeat the field* options multiple times.\n```"
					);
					return;
				}

				bool debug = false;
				SocketTextChannel channel = e.Channel;
				EmbedFieldBuilder currentField = null;
				EmbedBuilder embedBuilder = new EmbedBuilder();

				foreach( Match match in this.EmbedParamRegex.Matches(e.TrimmedMessage) )
				{
					string optionString = this.EmbedOptionRegex.Match(match.Value).Value;

					if( optionString == "--debug" )
					{
						if( this.Client.IsGlobalAdmin(e.Message.Author.Id) || this.Client.IsSupportTeam(e.Message.Author.Id) )
							debug = true;
						continue;
					}

					if( optionString == "--fieldInline" )
					{
						if( currentField == null )
						{
							await e.SendReplySafe($"`fieldInline` can not precede `fieldName`.");
							return;
						}

						currentField.WithIsInline(true);
						if( debug )
							await e.SendReplySafe($"Setting inline for field `{currentField.Name}`");
						continue;
					}

					string value;
					if( match.Value.Length <= optionString.Length || string.IsNullOrWhiteSpace(value  = match.Value.Substring(optionString.Length + 1).Trim()) )
					{
						await e.SendReplySafe($"Invalid value for `{optionString}`");
						return;
					}

					if( value.Length >= UserProfileOption.ValueCharacterLimit )
					{
						await e.SendReplySafe($"`{optionString}` is too long! (It's {value.Length} characters while the limit is {UserProfileOption.ValueCharacterLimit})");
						return;
					}

					switch(optionString)
					{
						case "--channel":
							if( !guid.TryParse(value.Trim('<', '>', '#'), out guid id) || (channel = e.Server.Guild.GetTextChannel(id)) == null )
							{
								await e.SendReplySafe($"Channel {value} not found.");
								return;
							}
							if( debug )
								await e.SendReplySafe($"Channel set: `{channel.Name}`");

							break;
						case "--title":
							if( value.Length > 256 )
							{
								await e.SendReplySafe($"`--title` is too long (`{value.Length} > 256`)");
								return;
							}

							embedBuilder.WithTitle(value);
							if( debug )
								await e.SendReplySafe($"Title set: `{value}`");

							break;
						case "--description":
							if( value.Length > 2048 )
							{
								await e.SendReplySafe($"`--description` is too long (`{value.Length} > 2048`)");
								return;
							}

							embedBuilder.WithDescription(value);
							if( debug )
								await e.SendReplySafe($"Description set: `{value}`");

							break;
						case "--footer":
							if( value.Length > 2048 )
							{
								await e.SendReplySafe($"`--footer` is too long (`{value.Length} > 2048`)");
								return;
							}

							embedBuilder.WithFooter(value);
							if( debug )
								await e.SendReplySafe($"Description set: `{value}`");

							break;
						case "--image":
							try
							{
								embedBuilder.WithImageUrl(value.Trim('<', '>'));
							}
							catch( Exception )
							{
								await e.SendReplySafe($"`--image` is invalid url");
								return;
							}

							if( debug )
								await e.SendReplySafe($"Image URL set: `{value}`");

							break;
						case "--thumbnail":
							try
							{
								embedBuilder.WithThumbnailUrl(value.Trim('<', '>'));
							}
							catch( Exception )
							{
								await e.SendReplySafe($"`--thumbnail` is invalid url");
								return;
							}

							if( debug )
								await e.SendReplySafe($"Thumbnail URL set: `{value}`");

							break;
						case "--color":
							uint color = uint.Parse(value.TrimStart('#'), System.Globalization.NumberStyles.AllowHexSpecifier);
							if( color > uint.Parse("FFFFFF", System.Globalization.NumberStyles.AllowHexSpecifier) )
							{
								await e.SendReplySafe("Color out of range.");
								return;
							}

							embedBuilder.WithColor(color);
							if( debug )
								await e.SendReplySafe($"Color `{value}` set.");

							break;
						case "--fieldName":
							if( value.Length > 256 )
							{
								await e.SendReplySafe($"`--fieldName` is too long (`{value.Length} > 256`)\n```\n{value}\n```");
								return;
							}

							if( currentField != null && currentField.Value == null )
							{
								await e.SendReplySafe($"Field `{currentField.Name}` is missing a value!");
								return;
							}

							if( embedBuilder.Fields.Count >= 25 )
							{
								await e.SendReplySafe("Too many fields! (Limit is 25)");
								return;
							}

							embedBuilder.AddField(currentField = new EmbedFieldBuilder().WithName(value));
							if( debug )
								await e.SendReplySafe($"Creating new field `{currentField.Name}`");

							break;
						case "--fieldValue":
							if( value.Length > 1024 )
							{
								await e.SendReplySafe($"`--fieldValue` is too long (`{value.Length} > 1024`)\n```\n{value}\n```");
								return;
							}

							if( currentField == null )
							{
								await e.SendReplySafe($"`fieldValue` can not precede `fieldName`.");
								return;
							}

							currentField.WithValue(value);
							if( debug )
								await e.SendReplySafe($"Setting value:\n```\n{value}\n```\n...for field:`{currentField.Name}`");

							break;
						default:
							await e.SendReplySafe($"Unknown option: `{optionString}`");
							return;
					}
				}

				if( currentField != null && currentField.Value == null )
				{
					await e.SendReplySafe($"Field `{currentField.Name}` is missing a value!");
					return;
				}

				await channel.SendMessageAsync(embed: embedBuilder.Build());
			};
			commands.Add(newCommand);

			return commands;
		}

		public async Task Update(IValkyrjaClient iClient)
		{
			ValkyrjaClient client = iClient as ValkyrjaClient;
			ServerContext dbContext = ServerContext.Create(client.DbConnectionString);
			bool save = false;
			//DateTime minTime = DateTime.MinValue + TimeSpan.FromMinutes(1);

			//Channels
			List<ChannelConfig> channelsToRemove = new List<ChannelConfig>();
			foreach( ChannelConfig channelConfig in dbContext.Channels.AsQueryable().Where(c => c.Temporary ) )
			{
				Server server;
				if( !client.Servers.ContainsKey(channelConfig.ServerId) ||
				    (server = client.Servers[channelConfig.ServerId]) == null )
					continue;

				//Temporary voice channels
				SocketGuildChannel channel = server.Guild.GetChannel(channelConfig.ChannelId);
				if( channel != null &&
				    channel.CreatedAt < DateTimeOffset.UtcNow - this.TempChannelDelay &&
				    !channel.Users.Any() )
				{
					try
					{
						await channel.DeleteAsync();
						channelConfig.Temporary = false;
						channelsToRemove.Add(channelConfig);
						save = true;
					}
					catch( HttpException e )
					{
						await server.HandleHttpException(e, $"Failed to delete temporary voice channel `{channel.Name}`");
					}
					catch( Exception e )
					{
						await this.HandleException(e, "Delete temporary voice channel", server.Id);
					}
				}
				else if( channel == null )
				{
					channelConfig.Temporary = false;
					channelsToRemove.Add(channelConfig);
					save = true;
				}
			}

			if( channelsToRemove.Any() )
				dbContext.Channels.RemoveRange(channelsToRemove);

			if( save )
				dbContext.SaveChanges();
			dbContext.Dispose();
		}
	}
}
