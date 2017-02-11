using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.Entities;
using RedditSharp;

using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class Reddit: IModule
	{
		protected static Reddit Instance = null;
		public static Reddit Get(){ return Instance; }


		public const string VerifySent = "<@{0}>, check your messages!";
		public const string VerifyDonePM = "You have been verified on the `{0}` server =]";
		public const string VerifyDone = "<@{0}> has been verified.";
		public const string VerifyError = "If you want me to send a PM with the info to someone, you have to @mention them (only one person though)";
		public const string UserNotFound = "I couldn't find them :(";

		private RedditSharp.Reddit RedditClient = null;
		private string LastVerifyMessage = "";


		public List<Command> Init<TUser>(IBotwinderClient<TUser> client) where TUser : UserData, new()
		{
			List<Command> commands;
			Command newCommand;

			if( !client.GlobalConfig.GiveawaysEnabled )
			{
				commands = new List<Command>();
				newCommand = new Command("verify");
				newCommand.Type = Command.CommandType.ChatOnly;
				newCommand.Description = "Participate in currently active giveaway.";
				newCommand.OnExecute += async (sender, e) => {
					await e.Message.Channel.SendMessage("Reddit verification is currently disabled for technical difficulties. Please be patient, we are working on it.");
				};
				commands.Add(newCommand);
				return commands;
			}

			commands = new List<Command>();

			try
			{
				if( client.GlobalConfig.RedditEnabled )
				{
					Console.WriteLine("Reddit: Connecting...");
					//BotWebAgent agent = new BotWebAgent(client.GlobalConfig.RedditUsername, client.GlobalConfig.RedditPassword, client.GlobalConfig.RedditClientId, client.GlobalConfig.RedditClientSecret, client.GlobalConfig.RedditRedirectUri);
					//this.RedditClient = new RedditSharp.Reddit(agent, true);
					this.RedditClient = new RedditSharp.Reddit(client.GlobalConfig.RedditUsername, client.GlobalConfig.RedditPassword);
					Console.WriteLine("Reddit: Connected.");
				}
			} catch(Exception e)
			{
				if( HandleException != null )
					HandleException(this, new ModuleExceptionArgs(e, "Module.Reddit.Init failed to connect RedditClient"));
				else
					throw;
			}

			Instance = this;

// !verify
			newCommand = new Command("verify");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "This will send you some info about verification. You can use this with a parameter to send the info to your friend - you have to @mention them.";
			newCommand.OnExecute += async (sender, e) =>{
				if( (!e.Server.ServerConfig.VerifyEnabled || e.Server.ServerConfig.VerifyRoleID == 0) && !client.IsGlobalAdmin(e.Message.User) )
				{
					await e.Message.Channel.SendMessage("Verification is disabled on this server.");
					return;
				}

				Discord.User user = null;
				Server<UserData> server = e.Server as Server<UserData>;

				// GlobalAdmin verified someone somewhere.
				if( e.MessageArgs != null && e.MessageArgs.Length == 3 && server.IsGlobalAdmin(e.Message.User) )
				{
					if( e.Message.RawText == this.LastVerifyMessage )
						return;

					this.LastVerifyMessage = e.Message.RawText;

					guid serverID, userID;
					Discord.Server discordServer = null;
					if( !guid.TryParse(e.MessageArgs[0], out serverID) || (discordServer = client.GetServer(serverID)) == null || !guid.TryParse(e.MessageArgs[1], out userID) || (user = discordServer.GetUser(userID)) == null  )
					{
						await e.Message.Channel.SendMessage(UserNotFound);
						return;
					}

					await VerifyUser(user, client.GetServerData(serverID), (e.MessageArgs[2] == "force" ? null : e.MessageArgs[2]));
					await e.Message.Channel.SendMessage(string.Format(VerifyDone, user.Id));
					return;
				}

				// Admin verified someone.
				if( e.MessageArgs != null && e.MessageArgs.Length == 2 && server.IsAdmin(e.Message.User) )
				{
					if( e.Message.RawText == this.LastVerifyMessage )
						return;

					this.LastVerifyMessage = e.Message.RawText;

					guid id;
					if( (e.Message.MentionedUsers.Count() != 1 || (user = e.Message.MentionedUsers.ElementAt(0)) == null) && (!guid.TryParse(e.MessageArgs[0], out id) || (user = e.Message.Server.GetUser(id)) == null) )
					{
						await e.Message.Channel.SendMessage(UserNotFound);
						return;
					}

					await VerifyUser(user, server, (e.MessageArgs[1] == "force" ? null : e.MessageArgs[1]));
					await e.Message.Channel.SendMessage(string.Format(VerifyDone, user.Id));
					return;
				}

				// Verify the author.
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					user = e.Message.User;
				}

				// Verify mentioned user.
				if( e.Message.MentionedUsers != null && e.Message.MentionedUsers.Count() == 1 )
				{
					user = e.Message.MentionedUsers.ElementAt(0);
				}

				if( user == null )
				{
					await e.Message.Channel.SendMessage(VerifyError);
				}
				else
				{
					if( await VerifyUserPM(user, server) )
						await e.Message.Channel.SendMessage(string.Format(VerifyDone, user.Id));
					else
						await e.Message.Channel.SendMessage(string.Format(VerifySent, user.Id));
				}
			};
			commands.Add(newCommand);

// !reddit
			newCommand = new Command("reddit");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "Print information about current reddit user.";
			newCommand.RequiredPermissions = Command.PermissionType.OwnerOnly;
			newCommand.OnExecute += async (sender, e) =>{
				string message = "";
				if( this.RedditClient == null )
					message = "_Reddit == null";
				else if( this.RedditClient.User == null )
					message = "_Reddit.User == null";
				else
				{
					message = string.Format("Mail: {0}\nModMail: {1}\nMessages: {2}", this.RedditClient.User.HasMail, (this.RedditClient.User.HasModMail ? this.RedditClient.User.ModMail.Count() : 0), this.RedditClient.User.UnreadMessages.Count());
				}

				await e.Message.Channel.SendMessage(message);
			};
			commands.Add(newCommand);


			return commands;
		}


		public async Task Update<TUser>(IBotwinderClient<TUser> client) where TUser : UserData, new()
		{
			if( !client.GlobalConfig.RedditEnabled )
				return;

			try
			{
				if( this.RedditClient == null || this.RedditClient.User == null || this.RedditClient.User.UnreadMessages != null )
					return;

				Discord.Server mainServer = client.GetServer(client.GlobalConfig.MainServerID);
				if( mainServer == null )
					return;
				Discord.Channel mainChannel = mainServer.GetChannel(client.GlobalConfig.MainLogChannelID);
				if( mainChannel == null )
					return;

				foreach(RedditSharp.Things.Thing thing in this.RedditClient.User.UnreadMessages)
				{
					RedditSharp.Things.PrivateMessage message = thing as RedditSharp.Things.PrivateMessage;
					if( message == null )
						continue;

					if( !string.IsNullOrWhiteSpace(message.Subject) && !string.IsNullOrWhiteSpace(message.Body)
						&& !message.IsComment && message.Subject.Contains("DiscordVerification") )
					{
						string[] ids = Regex.Split(Regex.Match(message.Body, "\\d+[\\s%]\\d+").Value, "(\\s|%20)");
						guid serverID = 0;
						guid userID = 0;
						Server<TUser> server = null;
						Discord.Server discordServer = null;
						Discord.User user = null;
						string link = message.Author.StartsWith("http") ? message.Author : message.Author.StartsWith("/u/") ? "https://www.reddit.com/user/" + message.Author.Remove(0, 3) : "https://www.reddit.com/user/" + message.Author;

						if( ids != null && ids.Length == 2 && guid.TryParse(ids[0], out serverID) && guid.TryParse(ids[1], out userID) &&
						    (discordServer = client.GetServer(serverID)) != null && client.Servers.ContainsKey(serverID) &&
						    (server = client.GetServerData(serverID)) != null && (user = discordServer.GetUser(userID)) != null )
						{
							await VerifyUser(user, server, link);

							//await message.SetAsReadAsync();
							message.SetAsRead();
						}
						else
						{
							//await message.ReplyAsync("Hi!\n I'm sorry but something went wrong with the reddit message (or discord servers) and I couldn't verify you... I did however notify Rhea (my mum!) and she will take care of it!\nI would like to ask you for patience, she may not be online =]");
							//message.Reply("Hi!\n I'm sorry but something went wrong with the reddit message (or discord servers) and I couldn't verify you... I did however notify Rhea (my mum!) and she will take care of it!\nI would like to ask you for patience, she may not be online =]");
							//await mainChannel.SendMessage(string.Format("Invalid DiscordVerification message received.\n    Author: {0}\n    Subject: {1}\n    Message: {2}\n    Found User: <@{3}>\n    Link to Author: {4}", message.Author, message.Subject, message.Body, user == null ? "null" : user.Id.ToString(), link));
						}
					}
					else if( !message.IsComment )
					{
						await mainChannel.SendMessage(string.Format("I received an unknown private message on reddit:\n{0}: {1}\n{2}", message.Author, message.Subject, message.Body));

						//await message.SetAsReadAsync();
						message.SetAsRead();
					}
				}
			} catch(Exception e)
			{
				if( e.GetType() != typeof(System.Net.WebException) )
					if( HandleException != null )
						HandleException(this, new ModuleExceptionArgs(e, "Module.Reddit.Update failed"));
					else
						throw;
			}
		}

		//Returns true if the user is already verified.
		public async Task<bool> VerifyUserPM<TUser>(Discord.User user, Server<TUser> server) where TUser : UserData, new()
		{
			TUser userData = null;
			if( server.UserDatabase.TryGetValue(user.Id, out userData) && userData.Verified )
			{
				await VerifyUser(user, server);
				return true;
			}

			string message = string.Format("Hi {0},\n" +
			                               "the `{1}` server is using reddit verification.\n\n" +
			                               "{2}\n\n", user.Name, user.Server.Name, server.ServerConfig.VerifyPM);

			if( server.ServerConfig.VerifyKarma > 0 )
				message += string.Format("You will also get {0} `{1}{2}` for verifying!\n\n", server.ServerConfig.VerifyKarma, server.ServerConfig.CommandCharacter, server.ServerConfig.KarmaCurrency);

			string newMessage = string.Format("The only requirement is to have registered Discord account with valid email address, otherwise you may lose this status.\n" +
			                                  "In order to complete the verification, please send me this message on Reddit _(Do not change anything, just click the link and hit send.)_\n" +
			                                  "https://www.reddit.com/message/compose/?to=Botwinder&subject=DiscordVerification&message={0}%20{1}" +
			                                  "\n_(Please note that this will **not** work on **mobile** version of Reddit." +
			                                  "If you are on mobile, you have to 1. click the link, 2. click reddit menu, 3. click \"Desktop Site\", 4. hit \"send.\")_" +
			                                  "\n\nCheers! :smiley:",
				user.Server.Id, user.Id);

			if( newMessage.Length + message.Length > GlobalConfig.MessageCharacterLimit )
			{
				await user.SendMessage(message);
				message = "";
			}
			message += newMessage;

			await user.SendMessage(message);
			return false;
		}

		private async Task VerifyUser<TUser>(Discord.User user, Server<TUser> server, string verifiedInfo = null) where TUser : UserData, new()
		{
			Discord.Role verifiedRole = server.DiscordServer.GetRole(server.ServerConfig.VerifyRoleID);
			try
			{
				if( verifiedRole != null )
					await user.AddRoles(verifiedRole);
			} catch(Exception exception)
			{
				if( HandleException != null )
					HandleException(this, new ModuleExceptionArgs(exception, server.Name + ": Failed to assign VerifyRole to " + user.Name));
				else
					throw;
			}

			TUser userData = server.UserDatabase.GetOrAddUser(user);
			if( !userData.Verified && server.ServerConfig.VerifyKarma < int.MaxValue / 2f )
				userData.KarmaCount += server.ServerConfig.VerifyKarma;
			if( !string.IsNullOrEmpty(verifiedInfo) )
				userData.VerifiedInfo = verifiedInfo;

			userData.Verified = true;
			server.UserDatabase.SaveAsync();

			await user.SendMessage(string.Format(VerifyDonePM, server.DiscordServer.Name));
		}

		public event EventHandler<ModuleExceptionArgs> HandleException;
	}
}
