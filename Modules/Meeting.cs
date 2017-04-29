using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Botwinder.Entities;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class Meetings: IModule
	{
		private ConcurrentDictionary<guid, Meeting> MeetingsCache = new ConcurrentDictionary<guid, Meeting>();


		public List<Command> Init<TUser>(IBotwinderClient<TUser> client) where TUser : UserData, new()
		{
			List<Command> commands = new List<Command>();

// !meeting
			Command newCommand = new Command("meeting");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Manage a meeting that will create nice logs and meeting minutes on our website. For example see <http://botwinder.info/meetings/244607894165651457/Example%20meeting>.";
			newCommand.OnExecute += async (sender, e) =>{
				string invalidArguments = string.Format(
					"Use this command with the following arguments:\n" +
					"  `{0}{1} start meetingName` - Start a new meeting with a name - please do not use any date in it, this will be added for you.\n" +
					"  `{0}{1} topic message` - Set a topic for this section of the meeting.\n" +
					"  `{0}{1} info message` - Add a simple info message into the minutes.\n" +
					"  `{0}{1} idea message` - Add an idea into the minutes.\n" +
					"  `{0}{1} agreed message` - Add an agreement message into the minutes.\n" +
					"  `{0}{1} action message` - Add an action point into the minutes, this expects some mentioned people as to who is supposed to complete this.\n" +
					"  `{0}{1} end` - End the meeting and post a link to the minutes.\n" +
					"  `{0}{1} undo` - Undo the previous command (except `end`, that's final!)",
					e.Server.ServerConfig.CommandCharacter,
					e.Command.ID
				);

				if( e.MessageArgs == null || e.MessageArgs.Length == 0 ||
				    (e.MessageArgs.Length < 2 && e.MessageArgs[0] != "end" && e.MessageArgs[0] != "undo" && e.MessageArgs[0] != "reload") )
				{
					await e.Message.Channel.SendMessage(invalidArguments);
					return;
				}

				string response = "";
				Meeting meeting = this.MeetingsCache.ContainsKey(e.Message.Channel.Id) ? this.MeetingsCache[e.Message.Channel.Id] : null;
				if( meeting == null )
				{
					string path = Path.Combine(Meeting.Foldername, e.Message.Channel.Id.ToString());
					if( Directory.Exists(Meeting.Foldername) && Directory.Exists(path) )
					{
						DirectoryInfo dirChannel = new DirectoryInfo(path);
						foreach(DirectoryInfo dir in dirChannel.GetDirectories())
						{
							foreach(FileInfo file in dir.GetFiles())
							{
								if( file.Name.EndsWith(".json") )
								{
									Meeting m = Meeting.Load(e.Message.Channel.Id, dir.Name, file.Name.Remove(file.Name.LastIndexOf(".json")));
									if( m.OwnerID == e.Message.User.Id && m.IsActive )
									{
										if( this.MeetingsCache.ContainsKey(e.Message.Channel.Id) )
											this.MeetingsCache.Remove(e.Message.Channel.Id);
										this.MeetingsCache.Add(e.Message.Channel.Id, meeting = m);
									}
								}
							}
						}
					}
				}

				if( meeting == null && e.MessageArgs[0] != "start" && e.MessageArgs[0] != "reload" )
				{
					await e.Message.Channel.SendMessage(string.Format("I couldn't find open meeting. " +
					                                                  "If you did not start it, please have the owner of the meeting run any command, or `{0}{1} reload.`",
						e.Server.ServerConfig.CommandCharacter, e.Command.ID));
					return;
				}

				if( meeting != null && !meeting.IsActive && e.MessageArgs[0] != "start" )
				{
					await e.Message.Channel.SendMessage("Dude, that meeting already ended o_O");
					return;
				}

				switch(e.MessageArgs[0])
				{
				case "reload":
					if( meeting == null )
						response = "I did not find your meeting, you have to be in the same channel and stuff...";
					else
						response = "You can continue your meeting!";
					break;
				case "start":
					if( meeting != null && meeting.IsActive )
					{
						response = "There is another meeting in this channel, please consider closing it first!";
						break;
					}

					string trimmedMessage = e.TrimmedMessage.Substring(e.TrimmedMessage.IndexOf(e.MessageArgs[1]));
					string filename = "";
					for(int i = 0; true; i++)
					{
						filename =  string.Format("{0}_{1:00}", DateTime.UtcNow.ToString("yyyy-MM-dd"), i);
						if( !File.Exists(Path.Combine(Meeting.Foldername, e.Message.Channel.Id.ToString(), trimmedMessage, filename+".json")) )
							break;
					}
					meeting = Meeting.Load(e.Message.Channel.Id, trimmedMessage, filename);
					if( this.MeetingsCache.ContainsKey(e.Message.Channel.Id) )
						this.MeetingsCache[e.Message.Channel.Id] = meeting;
					else
						this.MeetingsCache.Add(e.Message.Channel.Id, meeting);

					response = string.Format("Your meeting __**{0}**__ has started, you can follow the minutes here: <{1}>", meeting.Start(e.Message), meeting.Url);
					break;
				case "topic":
					response = string.Format((string.IsNullOrWhiteSpace(meeting.CurrentTopic) ? "" : "Topic changed from: _\""+ meeting.CurrentTopic +"\"_\n") + "New topic: **{0}**", meeting.Topic(e.Message));
					break;
				case "link":
				case "info":
					meeting.Info(e.Message);
					break;
				case "idea":
					meeting.Idea(e.Message);
					break;
				case "agreed":
					meeting.Agreed(e.Message);
					break;
				case "action":
					meeting.Action(e.Message);
					break;
				case "end":
					meeting.End();
					await e.Message.Channel.SendMessage(string.Format("Your meeting __**{0}**__ ended, please wait a moment while I finish up the logs.", meeting.MeetingName));
					await Task.Delay(TimeSpan.FromMilliseconds(100));
					await meeting.AppendLog(e.Message);

					response = string.Format("Done, here you go! =)\nMinutes: <{0}>", meeting.Url);
					break;
				case "undo":
					string undo = meeting.Undo();
					response = string.IsNullOrWhiteSpace(undo) ? "I did not remove anything, you are at the beginning." : ("Removed: " + undo);
					break;
				default:
					response = invalidArguments;
					break;
				}

				if( !string.IsNullOrWhiteSpace(response) )
					await e.Message.Channel.SendMessage(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("meetings"));


			return commands;
		}

#pragma warning disable 1998, 0067
		public async Task Update<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new(){}
		public event EventHandler<ModuleExceptionArgs> HandleException;
#pragma warning restore 1998, 0067




		public class Meeting
		{
			public const string Foldername = "meetings";

			public bool IsActive = false;
			public guid StartMessageID;
			public guid ChannelID;
			public guid OwnerID;
			public string MeetingName = "";
			public string PreviousTopic = "";
			public string CurrentTopic = "";
			public List<string> Actions = new List<string>();

			public string Url{ get{ return "http://botwinder.info/" + Foldername + "/" + this.ChannelID.ToString() + "/" + this.MeetingName.Replace(" ", "%20") + "/meeting?date=" + this.Filename; } }


			[NonSerialized] private Object _Lock = new Object();
			[NonSerialized] public string Filename = "";
			[NonSerialized] private Log _MarkdownFile = null;


			protected Meeting()
			{
			}

			public static Meeting Load(guid channelID, string meetingName, string filename)
			{
				if( !Directory.Exists(Foldername) )
					Directory.CreateDirectory(Foldername);

				string path = Path.Combine(Foldername, channelID.ToString());
				if( !Directory.Exists(path) )
					Directory.CreateDirectory(path);

				path = Path.Combine(path, meetingName);
				if( !Directory.Exists(path) )
					Directory.CreateDirectory(path);

				string markdownPath = Path.Combine(path, filename + ".md");
				path = Path.Combine(path, filename + ".json");
				if( !File.Exists(path) )
				{
					string json = JsonConvert.SerializeObject(new Meeting(), Formatting.Indented);
					File.WriteAllText(path, json);
				}

				Meeting newConfig = JsonConvert.DeserializeObject<Meeting>(File.ReadAllText(path));
				newConfig.ChannelID = channelID;
				newConfig.MeetingName = meetingName;
				newConfig.Filename = filename;
				newConfig._MarkdownFile = new Log(markdownPath);

				return newConfig;
			}

			public void SaveAsync()
			{
				Task.Run(() => Save());
			}

			private void Save()
			{
				lock(this._Lock)
				{
					string json = JsonConvert.SerializeObject(this, Formatting.Indented);
					string path = Path.Combine(Foldername, this.ChannelID.ToString(), this.MeetingName, this.Filename + ".json");
					File.WriteAllText(path, json);
				}
			}

			/// <summary> Start a new meeting and return its name. </summary>
			public string Start(Discord.Message message)
			{
				this.IsActive = true;
				this.StartMessageID = message.Id;
				this.OwnerID = message.User.Id;

				string name = message.Text.Substring(message.Text.IndexOf(' ', message.Text.IndexOf(' ') + 1) + 1) + " (" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ")";
				this._MarkdownFile.LogMessageAsync("#" + name + "\n\n&nbsp;\n\n##Meeting minutes:", true);
				SaveAsync();
				return name;
			}

			/// <summary> Set a new topic and return it. </summary>
			public string Topic(Discord.Message message)
			{
				this.PreviousTopic = this.CurrentTopic;
				this.CurrentTopic = message.Text.Substring(message.Text.IndexOf(' ', message.Text.IndexOf(' ') + 1) + 1);

				this._MarkdownFile.LogMessageAsync("\n\n1. **" + this.CurrentTopic + "**", true);

				SaveAsync();
				return this.CurrentTopic;
			}

			public void Info(Discord.Message message)
			{
				this._MarkdownFile.LogMessageAsync(GetFormattedMessage(message), true);
			}

			public void Idea(Discord.Message message)
			{
				this._MarkdownFile.LogMessageAsync(GetFormattedMessage(message, "IDEA:"), true);
			}

			public void Agreed(Discord.Message message)
			{
				this._MarkdownFile.LogMessageAsync(GetFormattedMessage(message, "Agreed:"), true);
			}

			public void Action(Discord.Message message)
			{
				string line = GetFormattedMessage(message, "ACTION:");
				this._MarkdownFile.LogMessageAsync(line, true);
				this.Actions.Add(line);
				SaveAsync();
			}

			/// <summary> Undo the last entry in the meeting, and return it. </summary>
			public string Undo()
			{
				if( !this.IsActive )
					return "";

				string file = this._MarkdownFile.GetFile();
				int lastLineIndex = file.LastIndexOf('\n');
				if( lastLineIndex == -1 )
					return"";

				string removedLine = file.Substring(lastLineIndex);
				file = file.Remove(lastLineIndex);

				if( removedLine.Contains(this.CurrentTopic) )
				{
					this.CurrentTopic = this.PreviousTopic;
					this.PreviousTopic = "";
					for(int i = 0; i < 3; i++)
						file = file.Remove(file.LastIndexOf('\n'));
				}

				this._MarkdownFile.OverwriteFile(file);

				if( this.Actions.Contains(removedLine) )
				{
					this.Actions.Remove(removedLine);
				}

				SaveAsync();
				return removedLine;
			}

			public void End()
			{
				this.IsActive = false;
				string actions = "\n\n&nbsp;\n\n##Action Items:";
				foreach(string line in this.Actions)
				{
					actions += "\n" + line.Trim();
				}

				this._MarkdownFile.LogMessageAsync(actions, true);
				SaveAsync();
			}

			public async Task AppendLog(Discord.Message message)
			{
				this._MarkdownFile.LogMessageAsync("\n\n&nbsp;\n\n##Chat log:\n\n", true);

				guid lastRemoved = message.Id;
				Discord.Message[] chunk = null;
				List<Discord.Message> messages = new List<Discord.Message>();
				while((chunk = await message.Channel.DownloadMessages(100, lastRemoved, useCache: false)) != null && chunk.Length > 0 && messages.Count < GlobalConfig.ArchiveMessageLimit)
				{
					bool done = false;
					for(int i = 0; i < chunk.Length; i++)
					{
						messages.Add(chunk[i]);
						if( chunk[i].Id == this.StartMessageID )
						{
							done = true;
							break;
						}
					}

					lastRemoved = chunk.Last().Id;

					if( done )
						break;
				}

				await this._MarkdownFile.ArchiveList(messages, true, true);
			}

			private string GetFormattedMessage(Discord.Message message, string prefix = null)
			{
				string text = message.RawText.Substring(message.RawText.IndexOf(' ', message.RawText.IndexOf(' ') + 1) + 1);
				text = text.Replace("_", "\\_").Replace("*", "\\*");
				text = (string.IsNullOrEmpty(this.CurrentTopic) ? "\n" : "\n    ") + "* " + (string.IsNullOrWhiteSpace(prefix) ? "" : ("**" + prefix + "** ")) + text;
				text = text.Replace("<@!" + message.User.Id + ">", "__" + ((message.User.Nickname == null || string.IsNullOrWhiteSpace(message.User.Nickname)) ? message.User.Name : message.User.Nickname) + "__");

				foreach(Discord.User user in message.MentionedUsers)
				{
					text = text.Replace("<@!" + user.Id + ">", "__" + ((user.Nickname == null || string.IsNullOrWhiteSpace(user.Nickname)) ? user.Name : user.Nickname) + "__");
					text = text.Replace("<@" + user.Id + ">", "__" + ((user.Nickname == null || string.IsNullOrWhiteSpace(user.Nickname)) ? user.Name : user.Nickname) + "__");
				}

				foreach(Discord.Role role in message.MentionedRoles)
					text = text.Replace("<@&" + role.Id + ">", "`@" + role.Name + "`");

				foreach(Discord.Channel channel in message.MentionedChannels)
					text = text.Replace("<#" + channel.Id + ">", "`#" + channel.Name + "`");

				return text;
			}
		}
	}
}
