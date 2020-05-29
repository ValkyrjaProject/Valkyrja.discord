using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Valkyrja.modules
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

		private const string PmString = "Hi {0},\nthe `{1}` server is using verification.\n\n{2}\n\n";
		private const string PmKarmaString = "You will also get {0} `{1}{2}` for verifying!\n\n";
		private const string PmCodeInfoString = "In order to get verified, you must **reply to me in PM** with a hidden code within the below text. " +
		                                "Just the code by itself, do not add anything extra. Read the text and you will find the code.\n" +
		                                "_(Beware that this will expire in a few hours, " +
		                                "if it does simply run the `{0}verify` command in the server chat, " +
		                                "and re-send the code that you already found - it won't change.)_";
		private const string PmVerifiedString = "You have been verified on the `{0}` server =]";
		private const string PmHashErrorString = "```diff\n- Error!\n\n  " +
		                                         "Please get in touch with the server administrator and let them know, that their Verification PM is invalid " +
		                                         "(It may be either too short, or too long. The algorithm is looking for lines with at least 10 words.)```";

		private const string FailedPmString = "I was unable to send a PM - please enable PMs from server members!";
		private const string SentString = "Check your messages!";
		private const string MentionedString = "I've sent them the instructions.";
		private const string VerifiedString = "I haz verified {0}";
		private const string UserNotFoundString = "I couldn't find them :(";
		private const string InvalidParametersString = "Invalid parameters. Use without any parameters to verify yourself, or mention someone to send them the instructions.";

		private string[] CaptchaPms = new[]{
			"To prove that you're a human, tell me what animal is this?\n```\n" +
			"(\\_/)\n" +
			"(^_^)\n" +
			"@(\")(\")\n```\n",
			"To prove that you're a human, tell me what animal is this?\n```\n" +
			"(\\_/)\n" +
			"(=.=)\n" +
			"@(\")(\")\n```\n",
		};
		private string[] CaptchaValidAnswers = new[]{
			"rabbit",
			"hare",
			"bunny",
			"bunno",
			"bunneh",
			"hase", //de
			"kanin", //swe
			"zajac", //sk
			"zajic", //cz
			"kralik", //sk&cz
			"koniglia", //italian
			"koniglio", //italian
			"coniglietto", //italian
			"conejita", //spanish
			"conejito", //spanish
			"coneja", //spanish
			"conejo", //spanish
			"lapine", //french
			"lapin" //french
		};

		private Object DbLock{ get; set; } = new Object();

		private ValkyrjaClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

			this.Client.Events.UserJoined += OnUserJoined;
			this.Client.Events.MessageReceived += OnMessageReceived;

// !unverify
			Command newCommand = new Command("unverify");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Remove verified status from someone.";
			newCommand.RequiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.CodeVerificationEnabled && !e.Server.Config.CaptchaVerificationEnabled )
				{
					await e.SendReplyUnsafe("Verification is disabled on this server.");
					return;
				}

				string response = "Done.";
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);
				IRole role = e.Server.Guild.GetRole(e.Server.Config.VerifyRoleId);
				if( !mentionedUsers.Any() || role == null )
				{
					await e.SendReplyUnsafe(UserNotFoundString);
					dbContext.Dispose();
					return;
				}

				try
				{
					foreach( UserData userData in mentionedUsers )
					{
						userData.Verified = false;
						SocketGuildUser user = e.Server.Guild.GetUser(userData.UserId);
						if( user != null )
							await user.RemoveRoleAsync(role);
					}

					dbContext.SaveChanges();
				}
				catch( Exception )
				{
					response = "Invalid configuration or permissions.";
				}

				dbContext.Dispose();
				await e.SendReplyUnsafe(response);
			};
			commands.Add(newCommand);

// !verify
			newCommand = new Command("verify");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "This will send you some info about verification. You can use this with a parameter to send the info to your friend - you have to @mention them.";
			newCommand.OnExecute += async e => {
				if( !e.Server.Config.CodeVerificationEnabled && !e.Server.Config.CaptchaVerificationEnabled )
				{
					await e.SendReplyUnsafe("Verification is disabled on this server.");
					return;
				}

				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				List<UserData> mentionedUsers = this.Client.GetMentionedUsersData(dbContext, e);
				string response = InvalidParametersString;

				// Admin verified someone.
				if( e.MessageArgs != null && e.MessageArgs.Length > 1 &&
				    e.Server.IsAdmin(e.Message.Author as SocketGuildUser) &&
				    e.MessageArgs[e.MessageArgs.Length - 1] == "force" )
				{
					if( !mentionedUsers.Any() )
					{
						await e.SendReplyUnsafe(UserNotFoundString);
						dbContext.Dispose();
						return;
					}

					await VerifyUsers(e.Server, mentionedUsers); // actually verify people
					response = string.Format(VerifiedString, mentionedUsers.Select(u => u.UserId).ToMentions());
				}
				else if( string.IsNullOrEmpty(e.TrimmedMessage) ) // Verify the author.
				{
					UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);
					if( await VerifyUsersPm(e.Server, new List<UserData>{userData}) )
						response = SentString;
					else
						response = FailedPmString;
				}
				else if( mentionedUsers.Any() ) // Verify mentioned users.
				{
					if( await VerifyUsersPm(e.Server, mentionedUsers) )
						response = MentionedString;
					else
						response = FailedPmString;
				}

				if( mentionedUsers.Any() )
					dbContext.SaveChanges();

				dbContext.Dispose();
				await e.SendReplyUnsafe(response);
			};
			commands.Add(newCommand);


			return commands;
		}

		public async Task<bool> VerifyUsersPm(Server server, List<UserData> users)
		{
			bool success = false;
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

				string verifyPm = "";
				string value = "";
				if( server.Config.CaptchaVerificationEnabled )
				{
					verifyPm = this.CaptchaPms[Utils.Random.Next(0,this.CaptchaPms.Length)];
					value = "captcha";
				}
				else
				{
					verifyPm = string.Format(PmCodeInfoString, server.Config.CommandPrefix);
					int source = Math.Abs((userData.UserId.ToString() + server.Id).GetHashCode());
					int chunkNum = (int)Math.Ceiling(Math.Ceiling(Math.Log(source)) / 2);
					StringBuilder hashBuilder = new StringBuilder(chunkNum);
					for( int i = 0; i < chunkNum; i++ )
					{
						char c = (char)((source % 100) / 4 + 97);
						hashBuilder.Append(c);
						source = source / 100;
					}

					string hash = hashBuilder.ToString();
					hash = hash.Length > 5 ? hash.Substring(0, 5) : hash;

					string[] lines = server.Config.CodeVerifyMessage.Split('\n');
					string[] words = null;
					bool found = false;
					bool theOtherHalf = false;
					try
					{
						for( int i = Utils.Random.Next(lines.Length / 2, lines.Length); !theOtherHalf || i >= lines.Length / 2; i-- )
						{
							if( i <= lines.Length / 2 )
							{
								if( theOtherHalf )
									break;
								theOtherHalf = true;
								i = lines.Length - 1;
							}

							if( (words = lines[i].Split(' ')).Length > 10 )
							{
								int space = Math.Max(1, Utils.Random.Next(words.Length / 4, words.Length - 1));
								lines[i] = lines[i].Insert(lines[i].IndexOf(words[space], StringComparison.Ordinal) - 1, $" the secret is: {hash} ");

								hashBuilder = new StringBuilder();
								hashBuilder.AppendLine(verifyPm).AppendLine("");
								for( int j = 0; j < lines.Length; j++ )
									hashBuilder.AppendLine(lines[j]);
								verifyPm = hashBuilder.ToString();
								value = hash;
								found = true;
								break;
							}
						}
					}
					catch( Exception e )
					{
						// This is ignored because user. Send them a message to fix it.
						found = false;

						await this.HandleException(e, "VerifyUserPm.hash", server.Id);
					}

					if( !found )
					{
						verifyPm = PmHashErrorString;
					}
				}

				if( !string.IsNullOrEmpty(value) )
				{
					ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
					VerificationData data = dbContext.Verification.AsQueryable().FirstOrDefault(u => u.ServerId == server.Id && u.UserId == userData.UserId);
					if( data == null )
					{
						data = new VerificationData(){
							ServerId = userData.ServerId,
							UserId = userData.UserId,
							Value = value
						};
						dbContext.Verification.Add(data);
					}

					data.Value = value;
					dbContext.SaveChanges();
				}

				string message = string.Format(PmString, user.Username, server.Guild.Name, verifyPm);

				if( server.Config.VerifyKarma > 0 )
					message += string.Format(PmKarmaString, server.Config.VerifyKarma,
						server.Config.CommandPrefix, server.Config.KarmaCurrency);

				success = await this.Client.SendPmSafe(user, message);
			}

			if( alreadyVerified.Any() )
				await VerifyUsers(server, alreadyVerified, false);

			return success;
		}

		/// <summary> Actually verify someone - assign the roles and stuff. </summary>
		private async Task<bool> VerifyUsers(Server server, List<UserData> users, bool sendMessage = true)
		{
			IRole role = server.Guild.GetRole(server.Config.VerifyRoleId);
			if( role == null )
			{
				if( this.Client.GlobalConfig.LogDebug )
					Console.WriteLine("Verification: Role not set.");

				await this.HandleException(new ArgumentException("Role is null"), "Failed to assign verification role.", server.Id);
				return false;
			}

			bool verified = false;
			foreach( UserData userData in users )
			{
				SocketGuildUser user = server.Guild.GetUser(userData.UserId);
				if( user == null )
				{
					await this.HandleException(new ArgumentException("User is null"), "Failed to assign verification role.", server.Id);
					verified = true; //Remove them from the list.
					continue;
				}

				try
				{
					if( !userData.Verified )
						await this.Client.SendPmSafe(user, string.Format(PmVerifiedString, server.Guild.Name));

					await user.AddRoleAsync(role);
					if( this.Client.GlobalConfig.LogDebug )
						Console.WriteLine("Verification: Verified " + user.Username);

					if( !userData.Verified && server.Config.VerifyKarma < int.MaxValue / 2f )
						userData.KarmaCount += server.Config.VerifyKarma;

					userData.Verified = true;
					verified = true;
				}
				catch( HttpException e )
				{
					await server.HandleHttpException(e, $"Failed to verify user <@{user.Id}>");
				}
				catch( Exception e )
				{
					await HandleException(e, $"Failed to verify {user.Id}", server.Id);
				}
			}

			if( this.Client.GlobalConfig.LogDebug && !verified )
				Console.WriteLine("Verification: Not verified.");

			return verified;
		}

		/// <summary> Verifies a user based on a hashCode string and returns true if successful. </summary>
		private Task VerifyUserHash(SocketUser author, string msg)
		{
			if( this.CaptchaValidAnswers.Contains(msg) )
				msg = "captcha";

			lock( this.DbLock )
			{
				bool save = false;
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				try
				{
					IEnumerable<VerificationData> data = dbContext.Verification.AsQueryable().Where(v => v.UserId == author.Id).AsEnumerable().Where(v => v.Value == msg).ToList();

					if( !data.Any() )
					{
						dbContext.Dispose();
						return Task.CompletedTask;
					}

					foreach( VerificationData d in data )
					{
						d.Value = "done";
						save = true;

						try
						{
							this.Client.SendPmSafe(author, "Thank you, you will be verified soon\u2122").GetAwaiter().GetResult();
						if( !this.Client.Servers.ContainsKey(d.ServerId) )
							continue;

						Server server = this.Client.Servers[d.ServerId];
						UserData userData = dbContext.GetOrAddUser(server.Id, d.UserId);
						if( VerifyUsers(server, new List<UserData>{userData}).GetAwaiter().GetResult() )
							dbContext.Verification.Remove(d);
						}
						catch( Exception e )
						{
							this.HandleException(e, "Verification PM received inner", d.ServerId).GetAwaiter().GetResult();
						}
					}

					if( save )
					{
						dbContext.SaveChanges();
					}
				}
				catch( Exception e )
				{
					this.HandleException(e, "Verification PM received", 0).GetAwaiter().GetResult();
				}

				dbContext.Dispose();
			}
			return Task.CompletedTask;
		}

		private async Task OnUserJoined(SocketGuildUser user)
		{
			Server server;
			if( !this.Client.Servers.ContainsKey(user.Guild.Id) || (server = this.Client.Servers[user.Guild.Id]) == null )
				return;

			bool verifiedByAge = false;
			IRole role = null;
			if( server.Config.VerifyRoleId > 0 && server.Config.VerifyAccountAgeDays > 0 && server.Config.VerifyAccountAge && Utils.GetTimeFromId(user.Id) + TimeSpan.FromDays(server.Config.VerifyAccountAgeDays) < DateTime.UtcNow && (role = server.Guild.GetRole(server.Config.VerifyRoleId)) != null )
			{
				verifiedByAge = true;
				try
				{
					await user.AddRoleAsync(role);
				}
				catch( HttpException e )
				{
					await server.HandleHttpException(e, "Failed to assign the verification role.");
				}
				catch( Exception e )
				{
					await HandleException(e, "Verification by account age", server.Id);
				}
			}

			if( (server.Config.CodeVerificationEnabled || server.Config.CaptchaVerificationEnabled) && server.Config.VerifyOnWelcome && !verifiedByAge )
			{
				await Task.Delay(3000);
				lock( this.DbLock )
				{
					ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

					UserData userData = dbContext.GetOrAddUser(server.Id, user.Id);
					VerifyUsersPm(server, new List<UserData>{userData}).GetAwaiter().GetResult();

					dbContext.Dispose();
				}
			}
		}

		public Task Update(IValkyrjaClient iClient)
		{
			if( !this.Client.GlobalConfig.VerificationUpdateEnabled )
				return Task.CompletedTask;

			lock( this.DbLock )
			{
				bool save = false;
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				foreach( VerificationData data in dbContext.Verification.AsQueryable().Where(v => v.Value == "done").ToList() )
				{
					if( !this.Client.Servers.ContainsKey(data.ServerId) )
						continue;
					Server server = null;
					try
					{
						UserData userData = dbContext.UserDatabase.AsQueryable().FirstOrDefault(u => u.UserId == data.UserId && u.ServerId == data.ServerId);
						if( userData == null )
							continue;
						server = this.Client.Servers[data.ServerId];
						if( VerifyUsers(server, new List<UserData>{userData}).GetAwaiter().GetResult() )
						{
							save = true;
							dbContext.Verification.Remove(data);
						}
					}
					catch( HttpException e )
					{
						if( server != null )
							server.HandleHttpException(e, "While verifying someone.");
					}
					catch( Exception e )
					{
						HandleException(e, "verification update", data.ServerId).GetAwaiter().GetResult();
					}
				}

				if( save )
					dbContext.SaveChanges();
				dbContext.Dispose();
			}

			return Task.CompletedTask;
		}

		public async Task OnMessageReceived(SocketMessage message)
		{
			if( !(message.Channel is SocketDMChannel) ) //Not a PM
				return;

			await VerifyUserHash(message.Author, message.Content);
		}
	}
}
