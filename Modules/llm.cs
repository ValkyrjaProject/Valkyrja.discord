using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using guid = System.UInt64;
using Ollama;

namespace Valkyrja.modules
{
	public class Llm: IModule
	{
		private ValkyrjaClient Client;


		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		private Uri OllamaUri = new Uri("http://127.0.0.1:11434");
		private string OllamaModel = "hf.co/unsloth/Qwen3.5-4B-GGUF:UD-Q4_K_XL";
		private string ModPrompt = "You are a precise Discord Moderation Analyst. Evaluate the provided user context and output strictly structured, concise moderation guidance.\n\n"+
			"CORE RULES:\n"+
			"1. Output ONLY the specified fields. No introductory remarks, explanations of your process, or conversational filler.\n"+
			"2. Weight past infractions by recency (e.g., active escalation vs. stale warnings from months ago).\n"+
			"3. Keep all explanations direct and capped at 1–2 sentences.\n"+
			"4. Community rules are: No spam, no nsfw, no racial slurs, no technical advice sourced by AI, no shitposting, no memes, no insults or disrespect towards others, respect pronouns of others.\n\n"+
			"--- INPUT DATA ---\n"+
			"Flagged Message: {0}\n"+
			"Warning History: {1}\n\n"+
			"--- OUTPUT FORMAT ---\n"+
			"## 1. Classification & Audit\n"+
			"- Classification: [Moderation Issue | Community Support Issue] — [1-sentence explanation]\n"+
			"- Policy Violation: [Violates Discord ToS / Community Rules | No Violation] — [Specific rule or ToS clause]\n"+
			"- Watchlist Status: [Flag User | Do Not Flag] — [1-sentence reason]\n\n"+
			"## 2. Context & Pattern\n"+
			"- Assessment: [1-2 sentences on user standing, account tenure, and infraction recency]";

		private string ModPromptFunny = "You are an entertaining Discord Moderation Analyst. Evaluate the provided user context and output funny, entertaining, strictly structured, concise moderation guidance.\n\n"+
			"CORE RULES:\n"+
			"1. Output ONLY the specified fields. No introductory remarks, explanations of your process, or conversational filler.\n"+
			"2. Weight past infractions by recency (e.g., active escalation vs. stale warnings from months ago).\n"+
			"3. Keep all explanations direct and capped at 1–2 sentences.\n"+
			"4. Community rules are: No spam, no nsfw, no racial slurs, no technical advice sourced by AI, no shitposting, no memes, no insults or disrespect towards others, respect pronouns of others.\n\n"+
			"--- INPUT DATA ---\n"+
			"Warning History: {0}\n\n"+
			"--- OUTPUT FORMAT ---\n"+
			"## 1. Classification & Audit\n"+
			"- Classification: [Moderation Issue | Community Support Issue] — [1-sentence explanation]\n"+
			"- Policy Violation: [Violates Discord ToS / Community Rules | No Violation] — [Specific rule or ToS clause]\n"+
			"- Watchlist Status: [Flag User | Do Not Flag] — [1-sentence reason]\n\n"+
			"## 2. Context & Pattern\n"+
			"- Assessment: [1-2 sentences on user standing, account tenure, and infraction recency]";

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			this.Client.Events.MessageReceived += HandleMessage;
			List<Command> commands = new List<Command>();

// !ollamaUser
			Command newCommand = new Command("aiUser");
			newCommand.IsCoreCommand = true;
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				guid id = 0;
				string responseString = "Invalid parameters.";
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 ||
				    !guid.TryParse(e.MessageArgs[1], out id) )
				{
					if( !e.Message.MentionedUsers.Any() )
					{
						await e.SendReplySafe(responseString);
						return;
					}

					id = e.Message.MentionedUsers.First().Id;
				}

				GlobalContext dbContext = GlobalContext.Create(this.Client.DbConnectionString);
				AiUser aiUser = dbContext.AiUsers.AsQueryable().FirstOrDefault(s => s.UserId == id);
				switch(e.MessageArgs[0])
				{
					case "add":
						if( aiUser == null )
						{
							dbContext.AiUsers.Add(aiUser = new AiUser(){UserId = id});
							dbContext.SaveChanges();
						}

						responseString = "Done.";
						break;
					case "remove":
						if( aiUser == null )
						{
							responseString = "ID not found.";
							break;
						}

						dbContext.AiUsers.Remove(aiUser);
						dbContext.SaveChanges();

						responseString = "Done.";
						break;
					default:
						responseString = "Invalid keyword.";
						break;
				}

				this.Client.AiUsers = dbContext.AiUsers.AsEnumerable().Select(u => u.UserId).ToList();
				dbContext.Dispose();
				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("aiUser"));
			commands.Add(newCommand.CreateAlias("llmUser"));

// !ollamaPs
			newCommand = new Command("ollamaPs");
			newCommand.IsAiCommand = true;
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Execute an LLM prompt.";
			newCommand.ManPage = new ManPage("", "");
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string responseString = "I have failed you, I'm sorry :(";
				try{
					using OllamaClient ollama = new OllamaClient(baseUri: OllamaUri);
					PsResponse response = await ollama.PsAsync();
					StringBuilder stringBuilder = new StringBuilder();
					foreach(Ps entry in response.Models)
					{
						stringBuilder.AppendLine($"`{entry.Name}` = `{entry.SizeVram/1024.0f/1024.0f/1024.0f:0.00} GiB VRAM`");
					}

					if(stringBuilder.Length > 0)
						responseString = stringBuilder.ToString();
				}
				catch(Exception exception)
				{
					responseString = responseString + $"\n\n{exception.Message}";
				}

				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("aiPs"));
			commands.Add(newCommand.CreateAlias("llmPs"));

// !ollama
			newCommand = new Command("ollama");
			newCommand.IsAiCommand = true;
			newCommand.Type = CommandType.Operation;
			newCommand.Description = "Execute an LLM prompt.";
			newCommand.ManPage = new ManPage("<prompt>", "`<prompt>` - The text prompt to shove into the LLM.");
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string responseString = "I have failed you, I'm sorry :(";
				try{
					if( string.IsNullOrEmpty(e.TrimmedMessage) )
					{
						await e.SendReplySafe("Hmm?");
						return;
					}

					string prompt = e.TrimmedMessage + " (Keep it short.)";
					IMessage refMsg = null;
					if( e.Message.Reference != null && e.Message.Channel.Id == e.Message.Reference.ChannelId && e.Message.Reference.MessageId.IsSpecified && (refMsg = await e.Message.Channel.GetMessageAsync(e.Message.Reference.MessageId.Value)) != null )
					{
						prompt = $"{e.TrimmedMessage} (Keep it short.)\n\n{refMsg.Content}";
					}

					var httpClient = new System.Net.Http.HttpClient();
					httpClient.Timeout = TimeSpan.FromMinutes(10);
					using OllamaClient ollama = new OllamaClient(httpClient, baseUri: OllamaUri);
					Chat chat = ollama.Chat(this.OllamaModel);
					await e.SendReplySafe("Executing in VRAM...");

					ChatMessage message = await chat.SendAsync(message: e.TrimmedMessage);
					responseString = message.Content;
				}
				catch(Exception exception)
				{
					responseString = responseString + $"\n\n{exception.Message}";
				}

				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("ai"));
			commands.Add(newCommand.CreateAlias("llm"));

// !translate
			newCommand = new Command("translate");
			newCommand.IsAiCommand = true;
			newCommand.Type = CommandType.Operation;
			newCommand.Description = "Translate a replied-to message using LLM.";
			newCommand.ManPage = new ManPage("", "Reply-to a message to translate.");
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string responseString = "I have failed you, I'm sorry :(";
				try{
					IMessage refMsg = null;
					if( e.Message.Reference == null || e.Message.Channel.Id != e.Message.Reference.ChannelId || !e.Message.Reference.MessageId.IsSpecified || (refMsg = await e.Message.Channel.GetMessageAsync(e.Message.Reference.MessageId.Value)) == null )
					{
						await e.SendReplySafe("Hmm?");
						return;
					}

					var httpClient = new System.Net.Http.HttpClient();
					httpClient.Timeout = TimeSpan.FromMinutes(10);
					using OllamaClient ollama = new OllamaClient(httpClient, baseUri: OllamaUri);
					Chat chat = ollama.Chat(this.OllamaModel);
					await e.SendReplySafe("Let me pull up a dictionary...");


					bool tldr = e.Command.Id.ToLower() != "translateLong";
					ChatMessage message = await chat.SendAsync(message: $"Translate this message{(tldr ? " (keep it short)" : "")}: {refMsg.Content}");
					responseString = message.Content;
				}
				catch(Exception exception)
				{
					responseString = responseString + $"\n\n{exception.Message}";
				}

				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("translateLong"));

// !aiMod
			newCommand = new Command("aiMod");
			newCommand.IsAiCommand = true;
			newCommand.Type = CommandType.Operation;
			newCommand.Description = "Execute an LLM prompt.";
			newCommand.ManPage = new ManPage("<UserID>", "`<UserID>` - User ID or mention to look for.");
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string responseString = "I have failed you, I'm sorry :(";
				ServerContext dbContext = null;
				try{
					guid foundId = 0;
					if( e.MessageArgs.Length != 1 || !guid.TryParse(e.MessageArgs[0].Trim('<', '@', '!', '>'), out guid id) )
					{
						await e.SendReplySafe("Hmm?");
						return;
					}

					dbContext = ServerContext.Create(this.Client.DbConnectionString);
					UserData userData = dbContext.GetOrAddUser(e.Server.Id, foundId);
					string prompt = string.Format(this.ModPromptFunny, userData?.Notes ?? "none");

					IMessage refMsg = null;
					if( e.Message.Reference != null && e.Message.Channel.Id == e.Message.Reference.ChannelId && e.Message.Reference.MessageId.IsSpecified && (refMsg = await e.Message.Channel.GetMessageAsync(e.Message.Reference.MessageId.Value)) != null )
					{
						prompt = $"{e.TrimmedMessage} (Keep it short.)\n\n{refMsg.Content}";
					}

					var httpClient = new System.Net.Http.HttpClient();
					httpClient.Timeout = TimeSpan.FromMinutes(10);
					using OllamaClient ollama = new OllamaClient(httpClient, baseUri: OllamaUri);
					Chat chat = ollama.Chat(this.OllamaModel);
					await e.SendReplySafe("Executing in VRAM...");

					ChatMessage message = await chat.SendAsync(message: e.TrimmedMessage);
					responseString = message.Content;
				}
				catch(Exception exception)
				{
					responseString = responseString + $"\n\n{exception.Message}";
				}

				dbContext?.Dispose();
				await e.SendReplySafe(responseString);
			};
			commands.Add(newCommand);

			return commands;
		}

		public Task Update(IValkyrjaClient iClient)
		{
			return Task.CompletedTask;
		}

		private async Task HandleMessage(SocketMessage message)
		{
			if( !this.Client.IsGlobalAdmin(message.Author.Id) )
				return;

			if( message.Reference == null || message.Channel.Id != message.Reference.ChannelId || !message.Reference.MessageId.IsSpecified || !message.MentionedUsers.Any(u => u.Id == this.Client.DiscordClient.CurrentUser.Id) )
				return;

			bool thinkInstruction = this.Client.RegexThink.IsMatch(message.Content);
			bool moderationInstruction = this.Client.RegexThinkMod.IsMatch(message.Content);
			if( !thinkInstruction && !moderationInstruction)
				return;

			ServerContext dbContext = ServerContext.Create(this.Client.DbConnectionString);

			try{
				IMessage refMsg = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
				if( refMsg == null )
					return;

				string prompt = null;
				if( thinkInstruction )
					prompt = $"What do you think about this chat message and what it says? (Keep it short.) \n{refMsg.Content}";
				if( moderationInstruction )
				{
					if( refMsg.Author.Id == this.Client.DiscordClient.CurrentUser.Id )
						prompt = $"What do you think about this chat user? How should we moderate this behaviour further? (Keep it short.) \n{refMsg.Content}";
					else
					{
						IGuildChannel channel = message.Channel as IGuildChannel;
						UserData userData = channel == null ? null : dbContext.GetOrAddUser(channel.GuildId, refMsg.Author.Id);
						prompt = string.Format(this.ModPrompt, refMsg.Content, userData?.Notes ?? "none");
					}
				}

				var httpClient = new System.Net.Http.HttpClient();
				httpClient.Timeout = TimeSpan.FromMinutes(10);
				using OllamaClient ollama = new OllamaClient(httpClient, baseUri: OllamaUri);
				Chat chat = ollama.Chat(this.OllamaModel);
				await message.Channel.SendMessageSafe("Let me take a look...", messageReference: new MessageReference(message.Id, message.Channel.Id));

				ChatMessage chatmsg = await chat.SendAsync(message: prompt);
				await message.Channel.SendMessageSafe(chatmsg.Content, messageReference: new MessageReference(message.Id, message.Channel.Id));
			}
			catch(Exception e)
			{
				await message.Channel.SendMessageSafe($"Error: {e.Message}", messageReference: new MessageReference(message.Id, message.Channel.Id));
				return;
			}
		}
	}
}

