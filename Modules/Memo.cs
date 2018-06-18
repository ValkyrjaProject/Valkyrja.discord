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
	public class Memo: IModule
	{
		private const string MemoString = "Your {0} is:\n\n{1}";
		private const string MemoOtherString = "{0}'s {1} is:\n\n{2}";
		private const string NoMemoString = "You don't have a {0}.";
		private const string NoMemoOtherString = "{0} doesn't have a {1}.";
		private const string SetString = "All set:\n\n{0}";
		private const string ClearedString = "I've cleared your memo ~ rip. _\\*The bot presses F to pay respects.*_\n**F**";
		private const string NotFoundString = "User not found by that expression.";

		private BotwinderClient Client;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !memo
			Command newCommand = new Command("memo");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Display your memo. If used with a username or mention, it will display someone else' memo.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				string response = "";

				if( !string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					string expression = e.TrimmedMessage.ToLower();
					SocketUser user = e.Message.MentionedUsers.FirstOrDefault();
					if( user == null )
						user = e.Server.Guild.Users.FirstOrDefault(u => u.Username.ToLower() == expression || (u.Nickname != null && u.Nickname.ToLower() == expression));

					if( user == null )
					{
						response = NotFoundString;
						dbContext.Dispose();
					}
					else
					{
						UserData userData = dbContext.GetOrAddUser(e.Server.Id, user.Id);
						if( string.IsNullOrEmpty(userData.Memo) )
							response = string.Format(NoMemoOtherString, user.GetNickname(), e.CommandId);
						else
							response = string.Format(MemoOtherString, user.GetNickname(), e.CommandId, userData.Memo);
					}
				}
				else
				{
					UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);
					if( string.IsNullOrEmpty(userData.Memo) )
						response = string.Format(NoMemoString, e.CommandId);
					else
						response = string.Format(MemoString, e.CommandId, userData.Memo);
				}

				await e.SendReplySafe(response);
				dbContext.Dispose();
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("profile"));

// !setMemo
			newCommand = new Command("setMemo");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Set your memo.";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				string response = "";

				UserData userData = dbContext.GetOrAddUser(e.Server.Id, e.Message.Author.Id);
				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					userData.Memo = "";
					response = ClearedString;
				}
				else
				{
					userData.Memo = e.TrimmedMessage;
					response = string.Format(SetString, userData.Memo);
				}

				dbContext.SaveChanges();
				dbContext.Dispose();
				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("setmemo"));


			return commands;
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
