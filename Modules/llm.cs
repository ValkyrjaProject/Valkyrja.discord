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
		private readonly Regex RegexThink = new Regex(".*(think|thoughts).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			this.Client.Events.MessageReceived += HandleMessage;
			List<Command> commands = new List<Command>();

// !ollama
			Command newCommand = new Command("ollama");
			newCommand.IsCoreCommand = true;
			newCommand.IsSupportCommand = true;
			newCommand.Type = CommandType.Operation;
			newCommand.Description = "Execute an LLM prompt.";
			newCommand.ManPage = new ManPage("<prompt>", "`<prompt>` - The text prompt to shove into the LLM.");
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string reply = "I have failed you, I'm sorry :(";
				try{
					if( string.IsNullOrEmpty(e.TrimmedMessage) )
					{
						await e.SendReplySafe("Hmm?");
						return;
					}

					using OllamaClient ollama = new OllamaClient(baseUri: OllamaUri);
					Chat chat = ollama.Chat(this.OllamaModel);
					await e.SendReplySafe("Executing in VRAM...");

					ChatMessage message = await chat.SendAsync(message: e.TrimmedMessage);
					reply = message.Content;
				}
				catch(Exception exception)
				{
					reply = reply + $"\n\n{exception.Message}";
				}

				await e.SendReplySafe(reply);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("ai"));
			commands.Add(newCommand.CreateAlias("llm"));

// !ollamaPs
			newCommand = new Command("ollamaPs");
			newCommand.IsCoreCommand = true;
			newCommand.IsSupportCommand = true;
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "Execute an LLM prompt.";
			newCommand.ManPage = new ManPage("<prompt>", "`<prompt>` - The text prompt to shove into the LLM.");
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.OnExecute += async e => {
				string reply = "I have failed you, I'm sorry :(";
				try{
					using OllamaClient ollama = new OllamaClient(baseUri: OllamaUri);
					PsResponse response = await ollama.PsAsync();
					StringBuilder stringBuilder = new StringBuilder();
					foreach(Ps entry in response.Models)
					{
						stringBuilder.AppendLine($"`{entry.Name}` = `{entry.SizeVram/1024.0f/1024.0f/1024.0f:0.00} GiB VRAM`");
					}

					if(stringBuilder.Length > 0)
						reply = stringBuilder.ToString();
				}
				catch(Exception exception)
				{
					reply = reply + $"\n\n{exception.Message}";
				}

				await e.SendReplySafe(reply);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("aiPs"));
			commands.Add(newCommand.CreateAlias("llmPs"));

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

			if( message.Reference == null || message.Channel.Id != message.Reference.ChannelId || !message.Reference.MessageId.IsSpecified || !message.MentionedUsers.Any(u => u.Id == this.Client.DiscordClient.CurrentUser.Id) || !this.RegexThink.IsMatch(message.Content) )
				return;

			try{
				IMessage refMsg = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
				using OllamaClient ollama = new OllamaClient(baseUri: OllamaUri);
				Chat chat = ollama.Chat(this.OllamaModel);
				await message.Channel.SendMessageSafe("Let me take a look...", messageReference: new MessageReference(message.Id, message.Channel.Id));

				// ChatMessage chatmsg = await chat.SendAsync(message: refMsg.Content);
				// await message.Channel.SendMessageSafe(chatmsg.Content, messageReference: new MessageReference(message.Id, message.Channel.Id));
			}
			catch(Exception e)
			{
				await message.Channel.SendMessageSafe($"Error: {e.Message}", messageReference: new MessageReference(message.Id, message.Channel.Id));
				return;
			}
		}
	}
}

