using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Botwinder.Entities;

using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class Events: JsonModule<Events.EventList>
	{
		public class EventList
		{
			public Event[] Events;
		}

		public class Event
		{
			public guid ServerID = 0;
			public string Title = "";
			public Member[] Members;

			public Event(){}
			public Event(guid id, string title = "")
			{
				this.ServerID = id;
				this.Title = title;
			}

			public Member GetOrAddMember(guid id, bool checkedIn = false)
			{
				if( this.Members == null )
				{
					this.Members = new Member[1];
					this.Members[0] = new Member(id, checkedIn);
					return this.Members[0];
				}

				Member member = this.Members.FirstOrDefault(m => m.UserID == id);
				if( member == null )
				{
					Array.Resize(ref this.Members, this.Members.Length +1);
					return this.Members[this.Members.Length -1] = new Member(id, checkedIn);
				}

				member.CheckedIn = checkedIn;
				return member;
			}
		}
		public class Member: IComparer<Member>
		{
			public guid UserID = 0;
			public bool CheckedIn = false;
			public float Score = 0f;

			public Member(){}
			public Member(guid id, bool checkedIn = false)
			{
				this.UserID = id;
				this.CheckedIn = checkedIn;
			}

			public int Compare(Member e1, Member e2)
			{
				if( e1.Score > e2.Score )
					return 1;
				if( e1.Score < e2.Score )
					return -1;

				return 0;
			}
		}


		protected override string Filename => "events.json";


		private Dictionary<guid, Event> EventsDictionary;

		public Event this[guid id]
		{
			get{
				if( !this.EventsDictionary.ContainsKey(id) )
					return null;
				return this.EventsDictionary[id];
			}
			set{
				if( value == null && this.EventsDictionary.ContainsKey(id) )
					this.EventsDictionary.Remove(id);
				else if( value != null )
				{
					value.ServerID = id;
					if( this.EventsDictionary.ContainsKey(id) )
						this.EventsDictionary[id] = value;
					else
						this.EventsDictionary.Add(id, value);
				}
			}
		}


		public override List<Command> Init<TUser>(IBotwinderClient<TUser> client)
		{
			List<Command> commands;
			Command newCommand;

			if( !client.GlobalConfig.EventsEnabled )
			{
				commands = new List<Command>();
				newCommand = new Command("event");
				newCommand.Type = Command.CommandType.ChatOnly;
				newCommand.Description = "A system to help you run the best events - use the command to see more details.";
				newCommand.OnExecute += async (sender, e) => {
					await e.Message.Channel.SendMessage("Events are currently disabled for technical difficulties. Please be patient, we are working on it.");
				};
				commands.Add(newCommand);
				commands.Add(newCommand.CreateAlias("events"));
				return commands;
			}

			commands = base.Init<TUser>(client);

			this.EventsDictionary = new Dictionary<guid, Event>();
			if( this.Data != null && this.Data.Events != null )
			{
				foreach(Event e in this.Data.Events)
				{
					this.EventsDictionary.Add(e.ServerID, e);
				}
			}


// !event
			newCommand = new Command("event");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "A system to help you run the best events - use the command to see more details.";
			newCommand.OnExecute += async (sender, e) =>{
				string responseMessage = "I haz an error! <@"+ client.GlobalConfig.OwnerIDs[0] +"> save me!";
				string invalidParameters = string.Format("Invalid parameters. Usage:\n" +
				                                         "    `{0}{1} signup` - Sign up for current event.\n" +
				                                         "    `{0}{1} checkin` - Check yourself in.\n" +
				                                         "    `{0}{1} score` - Display your own score.\n",
					e.Server.ServerConfig.CommandCharacter, e.Command.ID
				);

				if( e.Server.IsAdmin(e.Message.User) || e.Server.IsModerator(e.Message.User) || e.Server.IsSubModerator(e.Message.User) )
					invalidParameters += string.Format(
						"Moderators:\n" +
						"    `{0}{1} new Title` - Open a new event with `Title` as it's name.\n" +
						"    `{0}{1} title Whatever` - Change the title of the current event to `Whatever`.\n" +
						"    `{0}{1} close` - Close the current event (Irreversible, wipes the results as well!)\n" +
						"    `{0}{1} signup @-mention` - Signup @-mentioned people.\n" +
						"    `{0}{1} checkin @-mention` - Check @-mentioned people in.\n" +
						"    `{0}{1} score value @-mention` - Give @-mentioned people score, where `value` is the number (points, time, whatever you wish - single number) This overwrites any previous score!\n" +
						"    `{0}{1} list` - Display list of all attendees.\n" +
						"    `{0}{1} list present` - Display list of attendees who are checked-in.\n" +
						"    `{0}{1} list missing` - Display list of attendees who are _not_ checked-in.\n" +
						"    `{0}{1} results az/za` - Display a sorted list of attendees who have a `score` - `az` for ascending or `za` for descending order.\n" +
						"_Note that `list` and `results` will mention people - if you do not wish to mention them, use it in hidden channel..._",
						e.Server.ServerConfig.CommandCharacter, e.Command.ID
					);

				bool save = false;

				if( e.MessageArgs == null || e.MessageArgs.Length < 1 )
				{
					responseMessage = invalidParameters;
				}
				else if( (!e.Server.IsAdmin(e.Message.User) && !e.Server.IsModerator(e.Message.User) && !e.Server.IsSubModerator(e.Message.User)) &&
				         (e.MessageArgs[0] == "new" || e.MessageArgs[0] == "title" || e.MessageArgs[0] == "close" || e.MessageArgs[0] == "list" || e.MessageArgs[0] == "results" ||
				          (e.Message.MentionedUsers.Count() > 0 && (e.MessageArgs[0] == "signup" || e.MessageArgs[0] == "checkin" || e.MessageArgs[0] == "score"))) ||
				         (!e.Server.IsGlobalAdmin(e.Message.User) && e.MessageArgs[0] == "reload"))
				{
					responseMessage = string.Format("I'm sorry, but only Moderators can use `{0}{1}{2}`", e.Server.ServerConfig.CommandCharacter, e.Command.ID, e.MessageArgs[0]);
					if( e.MessageArgs[0] == "signup" || e.MessageArgs[0] == "checkin" || e.MessageArgs[0] == "score" )
						responseMessage += " with mentions.";
				}
				else
				{
					try
					{
						switch(e.MessageArgs[0])
						{
						case "new":
							if( this[e.Server.ID] != null )
							{
								responseMessage = "An event is already open, you have to `close` it first.";
								break;
							}

							string title = "";
							if( e.MessageArgs.Length > 1 )
								title = e.TrimmedMessage.Substring(e.MessageArgs[0].Length + 1);

							this[e.Server.ID] = new Event(e.Server.ID, title);
							save = true;
							responseMessage = "Done.";
							break;
						case "title":
							if( this[e.Server.ID] == null )
							{
								responseMessage = "You have to start a `new` event first.";
								break;
							}

							if( e.MessageArgs.Length > 1 )
								this[e.Server.ID].Title = e.TrimmedMessage.Substring(e.MessageArgs[0].Length + 1);
							else
								this[e.Server.ID].Title = "";
							save = true;
							responseMessage = "Done.";
							break;
						case "close":
							this[e.Server.ID] = null;
							save = true;
							responseMessage = "Press F to pay respects to this event. <@"+ e.Message.User.Id +"> just killed it.";
							break;
						case "list":
						{
							if( e.MessageArgs.Length > 1 && e.MessageArgs[1] != "present" && e.MessageArgs[1] != "checkedin" && e.MessageArgs[1] != "missing" )
							{
								responseMessage = "Unknown parameters.";
								break;
							}
							if( this[e.Server.ID] == null || this[e.Server.ID].Members == null ||
							    this[e.Server.ID].Members.Length == 0 ||
							    (e.MessageArgs.Length > 1 && (e.MessageArgs[1] == "present" || e.MessageArgs[1] == "checkedin") && this[e.Server.ID].Members.Count(m =>  m.CheckedIn) == 0) ||
							    (e.MessageArgs.Length > 1 && (e.MessageArgs[1] == "missing") && this[e.Server.ID].Members.Count(m => !m.CheckedIn) == 0) )
							{
								responseMessage = "There is nothing to display.";
								break;
							}

							string newLine = "";
							responseMessage = string.Format("List of {0} members"+ (e.MessageArgs.Length > 1 && e.MessageArgs[1] == "missing" ? ":" : " and their score:"), e.MessageArgs.Length < 2 ? "signed-up" : e.MessageArgs[1]);
							for(int i = 0; i < this[e.Server.ID].Members.Length; i++)
							{
								if( e.MessageArgs.Length > 1 && ((e.MessageArgs[1] == "present" || e.MessageArgs[1] == "checkedin") && this[e.Server.ID].Members[i].CheckedIn) )
									newLine = string.Format("\n  <@{0}>: {1:#0.###}", this[e.Server.ID].Members[i].UserID, this[e.Server.ID].Members[i].Score);
								else if( e.MessageArgs.Length > 1 && (e.MessageArgs[1] == "missing" && !this[e.Server.ID].Members[i].CheckedIn) )
									newLine = string.Format("\n  <@{0}>", this[e.Server.ID].Members[i].UserID);
								else if( e.MessageArgs.Length < 2 )
									newLine = string.Format("\n  <@{0}> ({1}): {2:#0.###}", this[e.Server.ID].Members[i].UserID, this[e.Server.ID].Members[i].CheckedIn ? "checked-in" : "**not** checked-in", this[e.Server.ID].Members[i].Score);
								else
									newLine = "";

								if( newLine.Length + responseMessage.Length >= GlobalConfig.MessageCharacterLimit )
								{
									await e.Message.Channel.SendMessage(responseMessage);
									responseMessage = "";
								}
								responseMessage += newLine;
							}
						}
							break;
						case "results":
						{
							if( e.MessageArgs.Length < 2 || (e.MessageArgs[1] != "az" && e.MessageArgs[1] != "za" && e.MessageArgs[1] != "ascending" && e.MessageArgs[1] != "descending") )
							{
								responseMessage = "Unknown parameters. Please specify whether you wish for ascending or descending order.";
								break;
							}
							if( this[e.Server.ID] == null || this[e.Server.ID].Members == null || this[e.Server.ID].Members.Length == 0 )
							{
								responseMessage = "There is nothing to display.";
								break;
							}

							bool ascending = e.MessageArgs[1] == "az" || e.MessageArgs[1] == "ascending";
							Member[] sortedMembers = new Member[this[e.Server.ID].Members.Length];
							Array.Copy(this[e.Server.ID].Members, sortedMembers, this[e.Server.ID].Members.Length);
							sortedMembers = sortedMembers.Where(m => m.Score != 0f).ToArray();
							Array.Sort<Member>(sortedMembers, new Member());

							responseMessage = string.Format("Sorted list of members, in {0} order:", e.MessageArgs[1] == "az" ? "ascending" : e.MessageArgs[1] == "za" ? "descending" : e.MessageArgs[1]);
							for(int i = ascending ? 0 : sortedMembers.Length -1; (ascending && i < sortedMembers.Length) || (!ascending && i >= 0); i += ascending ? 1 : -1) // So much Fun!! :D
							{
								string newLine = string.Format("\n  <@{0}>: {1:#0.###}", sortedMembers[i].UserID, sortedMembers[i].Score);

								if( newLine.Length + responseMessage.Length >= GlobalConfig.MessageCharacterLimit )
								{
									await e.Message.Channel.SendMessage(responseMessage);
									responseMessage = "";
								}
								responseMessage += newLine;
							}
						}
							break;
						case "signup":
						{
							if( this[e.Server.ID] == null )
							{
								responseMessage = "We don't have an event right now, you're doomed to boredom!";
								break;
							}

							List<Discord.User> users = new List<Discord.User>(e.Message.MentionedUsers);
							if( users.Count == 0 )
								users.Add(e.Message.User);

							responseMessage = "";
							for(int i = 0; i < users.Count; i++)
							{
								this[e.Server.ID].GetOrAddMember(users[i].Id);
								responseMessage += string.Format((i == 0 ? "" : i == users.Count -1 ? " and " : ", ") + "<@{0}>", users[i].Id);
							}
							responseMessage += " signed up for "+ this[e.Server.ID].Title;
							save = true;
						}
							break;
						case "checkin":
						{
							if( this[e.Server.ID] == null )
							{
								responseMessage = "We don't have an event right now, you're doomed to boredom!";
								break;
							}

							List<Discord.User> users = new List<Discord.User>(e.Message.MentionedUsers);
							if( users.Count == 0 )
								users.Add(e.Message.User);

							responseMessage = "";
							for(int i = 0; i < users.Count; i++)
							{
								this[e.Server.ID].GetOrAddMember(users[i].Id, true);
								responseMessage += string.Format((i == 0 ? "" : i == users.Count -1 ? " and " : ", ") + "<@{0}>", users[i].Id);
							}
							responseMessage += " checked-in.";
							save = true;
						}
							break;
						case "score":
						{
							if( this[e.Server.ID] == null )
							{
								responseMessage = "We don't have an event right now, you're doomed to boredom!";
								break;
							}

							List<Discord.User> users = new List<Discord.User>(e.Message.MentionedUsers);
							if( users.Count == 0 )
							{
								Member member = null;
								if( this[e.Server.ID].Members == null || (member = this[e.Server.ID].Members.FirstOrDefault(m => m.UserID == e.Message.User.Id)) == null )
									responseMessage = string.Format("<@{0}>, you did not even sign up x_X", e.Message.User.Id);
								else
									responseMessage = string.Format("<@{0}>, your score is {1:#0.###}", member.UserID, member.Score);
								break;
							}

							float score = 0f;
							if( !float.TryParse(e.MessageArgs[1], out score) )
							{
								responseMessage = string.Format("That's a weird number o_o\nScore value has to be the first parameter after the keyword: `{0}{1} score 123 @-user`",
									e.Server.ServerConfig.CommandCharacter, e.Command.ID);
								break;
							}

							responseMessage = string.Format("Score of {0:#0.###} was given to ", score);
							for(int i = 0; i < users.Count; i++)
							{
								this[e.Server.ID].GetOrAddMember(users[i].Id, true).Score = score;
								responseMessage += string.Format((i == 0 ? "" : i == users.Count -1 ? " and " : ", ") + "<@{0}>", users[i].Id);
							}
							responseMessage += ".";
							save = true;
						}
							break;
						default:
							responseMessage = invalidParameters;
							break;
						}
					} catch(Exception exception)
					{
						responseMessage = "I haz an error! <@"+ client.GlobalConfig.OwnerIDs[0] +"> save me!";
						responseMessage += "\n"+ exception.Message;

					}
				}

				if( save )
					SaveAsync();
				await e.Message.Channel.SendMessage(responseMessage);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("events"));


			return commands;
		}

#pragma warning disable 1998
		public override async Task Update<TUser>(IBotwinderClient<TUser> client){}
#pragma warning restore 1998

		protected override void Save()
		{
			this.Data.Events = new Event[this.EventsDictionary.Count];
			this.EventsDictionary.Values.CopyTo(this.Data.Events, 0);

			base.Save();
		}
	}
}
