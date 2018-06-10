using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Quotes: IModule
	{
		private BotwinderClient Client;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !getQuote
			Command newCommand = new Command("getQuote");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Get a random quote, or a quote with specific id, oooor search for a quote by a specific user!";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				string response = "";

				IEnumerable<Quote> quotes = dbContext.Quotes.Where(q => q.ServerId == e.Server.Id);

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					int count = quotes.Count();
					Int64 id = Utils.Random.Next(0, count - 1);
					response = quotes.FirstOrDefault(q => q.Id == id)?.ToString();
				}
				else
				{
					Int64 id = 0;
					string username = "";
					string nickname = "";
					if( Int64.TryParse(e.TrimmedMessage, out id) )
					{
						response = quotes.FirstOrDefault(q => q.Id == id)?.ToString();
					}
					else
					{
						if( e.Message.MentionedUsers.Any() )
						{
							username = e.Message.MentionedUsers.FirstOrDefault()?.Username.ToLower();
							nickname = (e.Message.MentionedUsers.FirstOrDefault() as IGuildUser)?.Nickname?.ToLower();
						}
						else
						{
							username = e.MessageArgs[0].ToLower();
						}
						quotes = quotes.Where(q => q.Username.ToLower().Contains(username) ||
						                           (!string.IsNullOrEmpty(nickname) && q.Username.ToLower().Contains(nickname)));
						id = Utils.Random.Next(0, quotes.Count() - 1);
						response = quotes.Skip((int)id).FirstOrDefault()?.ToString();
					}
				}

				if( string.IsNullOrEmpty(response) )
					response = "I didn't find no such quote.";

				await this.Client.SendMessageToChannel(e.Channel, response);
				dbContext.Dispose();
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("quote"));

// !addQuote
			newCommand = new Command("addQuote");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Add a new quote! Use with a username or mention as the first parameter, and the text as second. (Or you can just use a message ID.)";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);
				string response = "I've no idea what are you trying to tell me.\nUse with a username or mention as the first parameter, and the text as second. (Or you can just use a message ID.)";
				Quote quote = null;

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					response = "Wut?";
				}
				else if( guid.TryParse(e.TrimmedMessage, out guid messageId) )
				{
					if( await e.Channel.GetMessageAsync(messageId) is SocketMessage message )
					{
						quote = new Quote(){
							CreatedTime = new DateTime(message.CreatedAt.Ticks),
							Username = message.Author.Username,
							Value = message.Content.Replace('`', '\'') +
							        (message.Attachments.Any() ? (" " + message.Attachments.FirstOrDefault()?.Url + " ") : "")
						};
					}
					else
						response = "Message not found.";
				}
				else if( e.MessageArgs.Length > 1 )
				{
					string username = e.Message.MentionedUsers.FirstOrDefault()?.Username ?? e.MessageArgs[0];
					quote = new Quote(){
						CreatedTime = DateTime.UtcNow,
						Username = username,
						Value = e.TrimmedMessage.Substring(e.MessageArgs[0].Length).Replace('`', '\'')
					};
				}

				if( quote != null )
				{
					response = "Unexpected database error :<";
					quote.ServerId = e.Server.Id;
					quote.Id = dbContext.Quotes.Count(q => q.ServerId == e.Server.Id);

					dbContext.Quotes.Add(quote);
					dbContext.SaveChanges();

					response = $"**Quote created with ID** `{quote.Id}`:\n{quote.ToString()}";
				}

				dbContext.Dispose();
				await this.Client.SendMessageToChannel(e.Channel, response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("addquote"));


			return commands;
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
