using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord.Net;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Botwinder.secure
{
    public class Verification : IModule
    {
        protected static Verification Instance = null;

        public static Verification Get()
        {
            return Instance;
        }

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

        public const string VerifySent = "<@{0}>, check your messages!";
        public const string VerifyDonePM = "You have been verified on the `{0}` server =]";
        public const string VerifyDone = "<@{0}> has been verified.";

        public const string VerifyError =
            "If you want me to send a PM with the info to someone, you have to @mention them (only one person though)";

        public const string UserNotFound = "I couldn't find them :(";

	    private string LastVerifyMessage = "";

	    private readonly ConcurrentDictionary<string, HashedValue> HashedValues = new ConcurrentDictionary<string, HashedValue>();

        public async Task<List<Command<TUser>>> Init<TUser>(IBotwinderClient<TUser> iClient)
            where TUser : UserData, new()
        {
            BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
            List<Command<TUser>> commands = new List<Command<TUser>>();

//!verify
            Command<TUser> newCommand = new Command<TUser>("verify");
            newCommand.Type = CommandType.Standard;
            newCommand.RequiredPermissions = PermissionType.Everyone;
            newCommand.SendTyping = true;
            newCommand.Description =
                "This will send you some info about verification. You can use this with a parameter to send the info to your friend - you have to @mention them.";
            newCommand.OnExecute += async e =>
            {
                if ((!e.Server.Config.VerificationEnabled || e.Server.Config.VerifyRoleId == 0) &&
                    !client.IsGlobalAdmin(e.Message.Author.Id))
                {
                    await e.Message.Channel.SendMessageSafe("Verification is disabled on this server.");
                    return;
                }

                SocketUser user = null;
                Server<UserData> server = e.Server as Server<UserData>;

	            // GlobalAdmin verified someone somewhere.
				if( e.MessageArgs != null && e.MessageArgs.Length == 3 && client.IsGlobalAdmin(e.Message.Author.Id) )
				{
					if( e.Message.Content == this.LastVerifyMessage )
						return;

					this.LastVerifyMessage = e.Message.Content;

					await VerifyUser(user, client.GetServerData(serverID), (e.MessageArgs[2] == "force" ? null : e.MessageArgs[2]));
					await e.Message.Channel.SendMessageSafe(string.Format(VerifyDone, user.Id));
					return;
				}

				// Admin verified someone.
				if( e.MessageArgs != null && e.MessageArgs.Length == 2 && server.IsAdmin(e.Message.Author as SocketGuildUser) )
				{
					if( e.Message.Content == this.LastVerifyMessage )
						return;

					this.LastVerifyMessage = e.Message.Content;

					guid id;
					if( (e.Message.MentionedUsers.Count() != 1 || (user = e.Message.MentionedUsers.ElementAt(0)) == null) &&
					    (!guid.TryParse(e.MessageArgs[0], out id) || (user = e.Server.Guild.GetUser(id)) == null) )
					{
						await e.Message.Channel.SendMessageSafe(UserNotFound);
						return;
					}

					await VerifyUser(user, server, (e.MessageArgs[1] == "force" ? null : e.MessageArgs[1]));
					await e.Message.Channel.SendMessageSafe(string.Format(VerifyDone, user.Id));
					return;
				}

				// Verify the author.
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					user = e.Message.Author;
				}

				// Verify mentioned user.
				if( e.Message.MentionedUsers != null && e.Message.MentionedUsers.Count() == 1 )
				{
					user = e.Message.MentionedUsers.ElementAt(0);
				}

				if( user == null )
				{
					await e.Message.Channel.SendMessageSafe(VerifyError);
				}
				else
				{
					if( await VerifyUserPM(user, server) )
						await e.Message.Channel.SendMessageSafe(string.Format(VerifyDone, user.Id));
					else
						await e.Message.Channel.SendMessageSafe(string.Format(VerifySent, user.Id));
				}

            };
            commands.Add(newCommand);

            return commands;
        }

        public Task Update<TUser>(IBotwinderClient<TUser> iClient) where TUser : UserData, new()
        {
            BotwinderClient<TUser> client = iClient as BotwinderClient<TUser>;
            throw new NotImplementedException();
        }

	    //Returns true if the user is already verified.
		public async Task<bool> VerifyUserPM<TUser>(SocketUser user, Server<TUser> server) where TUser : UserData, new()
		{
			TUser userData = null;
			if( server.UserDatabase.TryGetValue(user.Id, out userData) && userData.Verified )
			{
				await VerifyUser(user, server);
				return true;
			}

			string verifyPm = server.Config.VerifyMessage;

			int source = Math.Abs((user.Id.ToString() + server.Id).GetHashCode());
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
			if( !this.HashedValues.ContainsKey(hash) )
				this.HashedValues.Add(hash, new HashedValue(user.Id, server.Id));

			verifyPm = "In order to get verified, you must reply to me with a hidden code within the below rules. " +
					   "Just the code by itself, do not add anything extra. Read the rules and you will find the code.\n" +
					   "_(Beware that this will expire in a few hours, " +
					   "if it does simply run the `verify` command in the server chat, " +
					   "and re-send the code that you already found - it won't change.)_";

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

						hashBuilder = new StringBuilder(lines.Length + 1);
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

				if( HandleException != null )
					HandleException(this, new ModuleExceptionArgs(e, "Module.Verification.VerifyUserPM failed for "+ server.ID.ToString()));
			}

			if( !found )
			{
				verifyPm = "```diff\n- Error!\n\n  " +
						   "Please get in touch with the server administrator and let them know, that their Verification PM is invalid " +
						   "(It may be either too short, or too long. The algorithm is looking for lines with at least 10 words.)```";
			}

			string message = string.Format("Hi {0},\nthe `{1}` server is using code verification.\n\n{2}\n\n",
				user.Username, server.Guild.Name, verifyPm);

			if( server.Config.VerifyKarma > 0 )
				message += string.Format("You will also get {0} `{1}{2}` for verifying!\n\n", server.Config.VerifyKarma,
					server.Config.CommandPrefix, server.Config.KarmaCurrency);

			await user.SendMessageSafe(message);
			return false;
		}

		/// <summary>Verifies a user based on a hashCode string and returns true if successful.</summary>
		public async Task<bool> VerifyUserHash<TUser>(IBotwinderClient<TUser> client, SocketUser user, string hashCode)
			where TUser : UserData, new()
		{
			if( !this.HashedValues.ContainsKey(hashCode) || this.HashedValues[hashCode].UserId != user.Id )
				return false;

			Server<TUser> server = client.GetServerData(this.HashedValues[hashCode].ServerId);
			await VerifyUser(server.Guild.GetUser(user.Id), server, null);
			return true;
		}

		private async Task VerifyUser<TUser>(SocketUser user, Server<TUser> server, string verifiedInfo = null)
			where TUser : UserData, new()
		{
			if( (user as SocketGuildUser).Roles.FirstOrDefault(r => r.Id == server.Config.VerifyRoleId) != null )
				return;

			TUser userData = server.UserDatabase.GetOrAddUser(user);
			bool alreadyVerified = userData.Verified;
			if( !alreadyVerified && server.Config.VerifyKarma < int.MaxValue / 2f )
				userData.KarmaCount += server.Config.VerifyKarma;

			userData.Verified = true;
			server.UserDatabase.SaveAsync();
			string response = string.Format(VerifyDonePM, server.Guild.Name);

			try
			{
				SocketRole verifiedRole = server.Guild.GetRole(server.Config.VerifyRoleId);
				if( verifiedRole != null )
					await (user as SocketGuildUser).AddRoleAsync(verifiedRole);
			}
			catch( HttpException exception )
			{
				response = "I'm sorry but something went wrong. " + (exception.HttpCode == HttpStatusCode.Forbidden ? "Insufficient permissions to assign the role." : "Unknown error (random Discord derp?)" );

				if( HandleException != null )
					HandleException(this,
						new ModuleExceptionArgs(exception, server.Guild.Name + ": Failed to assign VerifyRole to " + user.Name));
				else
					throw;
			}

			try
			{
				if( !alreadyVerified )
					await user.SendMessageSafe(response);
			}
			catch( Exception ){}
		}

    }
}
