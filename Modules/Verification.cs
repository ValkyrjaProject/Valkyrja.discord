using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.WebSocket;

using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Verification: IModule
	{
		private class HashedValue
		{
			public guid UserId;
			public guid ServerId;

			public HashedValue(guid userId, guid serverId)
			{
				this.UserId = userId;
				this.ServerId = serverId;
			}
		}

		private const string PmString = "Hi {0},\nthe `{1}` server is using code verification.\n\n{2}\n\n";
		private const string PmKarmaString = "You will also get {0} `{1}{2}` for verifying!\n\n";
		private const string PmInfoString = "In order to get verified, you must reply to me with a hidden code within the below rules. " +
		                                "Just the code by itself, do not add anything extra. Read the rules and you will find the code.\n" +
		                                "_(Beware that this will expire in a few hours, " +
		                                "if it does simply run the `verify` command in the server chat, " +
		                                "and re-send the code that you already found - it won't change.)_";
		private const string PmVerifiedString = "You have been verified on the `{0}` server =]";
		private const string PmHashErrorString = "```diff\n- Error!\n\n  " +
		                                         "Please get in touch with the server administrator and let them know, that their Verification PM is invalid " +
		                                         "(It may be either too short, or too long. The algorithm is looking for lines with at least 10 words.)```";

		private const string SentString = "Check your messages!";
		private const string MentionedString = "I've sent them the instructions.";
		private const string VerifiedString = "I haz verified {0}";
		private const string UserNotFoundString = "I couldn't find them :(";
		private const string InvalidParametersString = "Invalid parameters. Use without any parameters to verify yourself, or mention someone to send them the instructions.";


		private BotwinderClient Client;
		private readonly ConcurrentDictionary<string, HashedValue> HashedValues = new ConcurrentDictionary<string, HashedValue>();


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.MessageReceived += async message => {
				if( message.Channel is IDMChannel )
					await VerifyUserHash(message.Author.Id, message.Content);
			};

// !verify
			Command newCommand = new Command("verify");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "This will send you some info about verification. You can use this with a parameter to send the info to your friend - you have to @mention them.";
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConfig.GetDbConnectionString());
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);
				string response = InvalidParametersString;

				// Admin verified someone.
				if( e.MessageArgs != null && e.MessageArgs.Length > 1 &&
				    e.Server.IsAdmin(e.Message.Author as SocketGuildUser) &&
				    e.MessageArgs[e.MessageArgs.Length - 1] == "force" )
				{
					if( !mentionedUsers.Any() )
					{
						await this.Client.SendMessageToChannel(e.Channel, UserNotFoundString);
						dbContext.Dispose();
						return;
					}

					await VerifyUsers(e.Server, mentionedUsers); // actually verify people
					response = string.Format(VerifiedString, mentionedUsers.Select(u => u.UserId).ToMentions());
					dbContext.SaveChanges();
				}
				else if( string.IsNullOrEmpty(e.TrimmedMessage) ) // Verify the author.
				{
					UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);
					await VerifyUsersPm(e.Server, new List<UserData>{userData});
					response = SentString;
				}
				else if( mentionedUsers.Any() ) // Verify mentioned users.
				{
					await VerifyUsersPm(e.Server, mentionedUsers);
					response = MentionedString;
				}

				await this.Client.SendMessageToChannel(e.Channel, response);
				dbContext.Dispose();
			};
			commands.Add(newCommand);


			return commands;
		}

		public async Task VerifyUsersPm(Server server, List<UserData> users)
		{
			List<UserData> alreadyVerified = new List<UserData>();
			foreach( UserData userData in users )
			{
				SocketGuildUser user = server.Guild.GetUser(userData.UserId);
				if( user == null )
					continue;

				if( userData.Verified )
				{
					alreadyVerified.Add(userData);
					continue;
				}

				string verifyPm = PmInfoString;
				int source = Math.Abs((userData.UserId.ToString() + server.Id).GetHashCode());
				int chunkNum = (int) Math.Ceiling(Math.Ceiling(Math.Log(source)) / 2);
				StringBuilder hashBuilder = new StringBuilder(chunkNum);
				for( int i = 0; i < chunkNum; i++ )
				{
					char c = (char) ((source % 100) / 4 + 97);
					hashBuilder.Append(c);
					source = source / 100;
				}
				string hash = hashBuilder.ToString();
				hash = hash.Length > 5 ? hash.Substring(0, 5) : hash;
				if( !this.HashedValues.ContainsKey(hash) )
					this.HashedValues.Add(hash, new HashedValue(userData.UserId, server.Id));

				string[] lines = server.Config.VerifyMessage.Split('\n');
				string[] words = null;
				bool found = false;
				try
				{
					for( int i = Utils.Random.Next(lines.Length / 2, lines.Length); i >= lines.Length / 2; i-- )
					{
						if( i <= lines.Length / 2 )
						{
							i = lines.Length - 1;
							continue;
						}
						if( (words = lines[i].Split(' ')).Length > 10 )
						{
							int space = Utils.Random.Next(words.Length / 4, words.Length - 1);
							lines[i] = lines[i].Insert(lines[i].IndexOf(words[space]) - 1, " the secret is: " + hash + " ");

							hashBuilder = new StringBuilder();
							hashBuilder.AppendLine(verifyPm).AppendLine("");
							for( int j = 0; j < lines.Length; j++ )
								hashBuilder.AppendLine(lines[j]);
							verifyPm = hashBuilder.ToString();
							found = true;
							break;
						}
					}
				}
				catch(Exception e)
				{
					// This is ignored because user. Send them a message to fix it.
					found = false;

					await this.HandleException(e, "VerifyUserPm.hash", server.Id);
				}

				if( !found )
				{
					verifyPm = PmHashErrorString;
				}

				string message = string.Format(PmString, user.Username, server.Guild.Name, verifyPm);
				if( server.Config.VerifyKarma > 0 )
					message += string.Format(PmKarmaString, server.Config.VerifyKarma,
						server.Config.CommandPrefix, server.Config.KarmaCurrency);

				await user.SendMessageSafe(message);
			}

			if( alreadyVerified.Any() )
				await VerifyUsers(server, alreadyVerified, false);
		}

		/// <summary> Actually verify someone - assign the roles and stuff. </summary>
		private async Task<bool> VerifyUsers(Server server, List<UserData> users, bool sendMessage = true)
		{
			IRole role = server.Guild.GetRole(server.Config.VerifyRoleId);
			if( role == null )
				return false;

			bool verified = false;
			foreach( UserData userData in users )
			{
				SocketGuildUser user = server.Guild.GetUser(userData.UserId);
				if( user == null )
					continue;

				try
				{
					await user.SendMessageSafe(string.Format(PmVerifiedString, server.Guild.Name));
					await user.AddRoleAsync(role);

					if( !userData.Verified && server.Config.VerifyKarma < int.MaxValue / 2f )
						userData.KarmaCount += server.Config.VerifyKarma;

					userData.Verified = true;
					verified = true;
				} catch(Exception) { }
			}

			return verified;
		}

		/// <summary> Verifies a user based on a hashCode string and returns true if successful. </summary>
		private async Task VerifyUserHash(guid userId, string hashCode)
		{
			if( !this.HashedValues.ContainsKey(hashCode) || this.HashedValues[hashCode].UserId != userId || !this.Client.Servers.ContainsKey(this.HashedValues[hashCode].ServerId) )
				return;

			Server server = this.Client.Servers[this.HashedValues[hashCode].ServerId];

			ServerContext dbContext = ServerContext.Create(this.Client.DbConfig.GetDbConnectionString());
			UserData userData = dbContext.GetOrAddUser(server.Id, userId);

			if( await VerifyUsers(server, new List<UserData>{userData}) )
				dbContext.SaveChanges();

			dbContext.Dispose();
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
