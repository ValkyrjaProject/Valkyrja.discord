using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Botwinder.Entities;
using Discord;
using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class Patchnotes: JsonModule<Patchnotes.SubscriberList>
	{
		public class SubscriberList
		{
			public guid[] Subscribers;
		}

		protected override string Filename => "Patchnotes.json";

		public override List<Command> Init<TUser>(IBotwinderClient<TUser> client)
		{
			List<Command> commands = base.Init<TUser>(client);
			Command newCommand;

// !subscribe
			newCommand = new Command("subscribe");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "Subscribe to patchnotes and maintenance notifications (you will be PMed these)";
			newCommand.OnExecute += async (sender, e) =>{
				if( this.Data.Subscribers == null || !this.Data.Subscribers.Contains(e.Message.User.Id) )
				{
					lock(this.Lock)
					{
						if( this.Data.Subscribers == null )
							this.Data.Subscribers = new guid[1];
						else
							Array.Resize(ref this.Data.Subscribers, this.Data.Subscribers.Length + 1);

						this.Data.Subscribers[this.Data.Subscribers.Length - 1] = e.Message.User.Id;
					}
				}

				SaveAsync();
				await e.Message.Channel.SendMessageSafe("I'll PM you with new features and maintenance notifications!");
			};
			commands.Add(newCommand);

// !unsubscribe
			newCommand = new Command("unsubscribe");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "Unsubscribe from the patchnotes and maintenance notifications.";
			newCommand.OnExecute += async (sender, e) =>{
				if( this.Data.Subscribers != null && this.Data.Subscribers.Contains(e.Message.User.Id) )
				{
					lock(this.Lock)
					{
						List<guid> list = this.Data.Subscribers.ToList();
						list.Remove(e.Message.User.Id);
						this.Data.Subscribers = list.ToArray();
					}
				}

				SaveAsync();
				await e.Message.Channel.SendMessageSafe("Your loss!");
			};
			commands.Add(newCommand);

// !patchnotes
			newCommand = new Command("patchnotes");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.IsCoreCommand = true;
			newCommand.Description = "See what new tricks I can do!";
			newCommand.OnExecute += async (sender, e) => {
				await e.Message.Channel.SendMessageSafe(GetPatchnotes());
			};
			commands.Add(newCommand);

// !sendPatchnotes
			newCommand = new Command("sendPatchnotes");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "PM the Patchnotes to all the subscribed users.";
			newCommand.RequiredPermissions = Command.PermissionType.OwnerOnly;
			newCommand.OnExecute += async (sender, e) => {
				if( this.Data.Subscribers == null || this.Data.Subscribers.Length == 0 )
				{
					await e.Message.Channel.SendMessage("Sorry, not enough subscribers to PM X_X");
					return;
				}

				string message = e.Command.ID == "sendPatchnotes" ? GetPatchnotes() : e.TrimmedMessage;
				lock(this.Lock)
				{
					foreach( ulong id in this.Data.Subscribers )
					{
						try
						{
							User user = null;
							List<Server> servers = new List<Server>(){ client.GetServer(client.GlobalConfig.MainServerID) };
							servers.AddRange(client.Clients.SelectMany(c => c.Servers));

							ulong id1 = id;
							if( servers.Any(s => (user = s.GetUser(id1)) != null) )
								user.SendMessageSafe(message).Wait();
						} catch(Exception exception)
						{
							client.LogException(exception, e, "User ID: " + id.ToString());
						}
					}
				}

				await e.Message.Channel.SendMessageSafe(message);
			};
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("pmSubscribed");
			newCommand.Description = "PM a custom message to all the subscribed users.";
			commands.Add(newCommand);

			return commands;
		}

#pragma warning disable 1998
		public override async Task Update<TUser>(IBotwinderClient<TUser> client){}
#pragma warning restore 1998

		private string GetPatchnotes()
		{
			if( !Directory.Exists("updates") || !File.Exists(Path.Combine("updates", "changelog")) )
				return "This is not the original <http://botwinder.info>, therefor I can not tell you, what's new here :<";

			string changelog = File.ReadAllText(Path.Combine("updates", "changelog"));
			int start = changelog.IndexOf("**Botwinder");
			int end = changelog.Substring(start+1).IndexOf("**Botwinder");

			if( start >= 0 && end <= changelog.Length && end > start && (changelog = changelog.Substring(start, end-start+"**Botwinder".Length)).Length > 0 )
				return changelog + "\n\nSee the full changelog and upcoming features at <http://botwinder.info/updates>!";

			return "There is an error in the data so I have failed to retrieve the patchnotes. Sorry mastah!";
		}
	}
}
