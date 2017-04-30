using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.Entities;

using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class Polls: JsonModule<Polls.PollList>
	{
		public class PollList
		{
			public Poll[] Polls;
		}

		public class Poll
		{
			public Vote[] Votes = null;
			public List<string> Options = new List<string>();

			public string Title = " ";
			public PollType Type = PollType.Strict;
			public guid ServerID = 0;

			public Poll(){}
			public Poll(guid serverID, PollType type)
			{
				this.ServerID = serverID;
				this.Type = type;
			}


			public ErrorCode AddVote(Discord.User member, string vote)
			{
				vote = vote.ToLower();
				if( this.Type == PollType.Free && !this.Options.Contains(vote) )
					this.Options.Add(vote);

				if( !this.Options.Contains(vote) )
					return ErrorCode.InvalidOption;

				if( this.Votes == null || this.Votes.Length == 0 )
				{
					this.Votes = new Vote[1];
				}
				else
				{
					Vote alreadyVoted = this.Votes.FirstOrDefault(v => v.MemberID == member.Id);
					if( alreadyVoted != null )
					{
						alreadyVoted.Voted = vote;
						return ErrorCode.AlreadyVoted;
					}

					Array.Resize<Vote>(ref this.Votes, this.Votes.Length + 1);
				}

				this.Votes[this.Votes.Length-1] = new Vote(member, vote);
				return ErrorCode.Success;
			}

			public void AddOption(string newOption)
			{
				newOption = newOption.ToLower();
				if( this.Options.Contains(newOption) )
					return;

				this.Options.Add(newOption);
			}

			public void EditOption(string oldOption, string newOption)
			{
				oldOption = oldOption.ToLower();
				newOption = newOption.ToLower();

				this.Options.Remove(oldOption);
				if( !this.Options.Contains(newOption) )
					this.Options.Add(newOption);

				if( this.Votes != null && this.Votes.Length > 0 )
					foreach(Vote v in this.Votes)
						if( v.Voted == oldOption )
							v.Voted = newOption;
			}

			public Result GetResults()
			{
				Result result = new Result();

				if( this.Votes != null )
				{
					SortedList<string, int> list = new SortedList<string, int>();
					foreach(Vote v in this.Votes)
					{
						if( !list.ContainsKey(v.Voted) )
							list.Add(v.Voted, 0);
						list[v.Voted]++;
					}
					result.SortAndAdd(list);
				}


				foreach(string option in this.Options)
					if( !result.ContainsKey(option) )
						result[option] = 0;

				return result;
			}
		}

		public class Result
		{
			private Dictionary<string, int> _Data = new Dictionary<string, int>();
			public Dictionary<string, int> Data{ get{ return this._Data; } }

			public int this[string key]
			{
				get{
					if( !this._Data.ContainsKey(key) )
						return 0;
					return this._Data[key];
				}
				set{
					if( !this._Data.ContainsKey(key) )
						this._Data.Add(key, 0);
					this._Data[key] = value;
				}
			}

			public bool ContainsKey(string key)
			{
				return this._Data.ContainsKey(key);
			}

			public void SortAndAdd(SortedList<string, int> data)
			{
				this._Data = data.OrderByDescending(p => p.Value).ToDictionary(p => p.Key, p => p.Value);
			}
		}

		public class Vote
		{
			public guid MemberID = 0;
			public string Voted = "";

			public Vote(){}
			public Vote(Discord.User member, string voted)
			{
				this.MemberID = member.Id;
				this.Voted = voted;
			}
		}

		public enum PollType
		{
			Strict,
			Custom,
			Free
		}

		public enum ErrorCode
		{
			Success = 0,
			AlreadyVoted,
			InvalidOption
		}


		protected override string Filename => "polls.json";


		private ConcurrentDictionary<guid, Poll> PollDictionary;

		public int Count(){ return this.PollDictionary.Count; }
		public bool ContainsKey(guid id)
		{
			return this.PollDictionary.ContainsKey(id);
		}
		public Poll this[guid id]
		{
			get{
				if( !this.PollDictionary.ContainsKey(id) )
					return null;
				return this.PollDictionary[id];
			}
			set{
				if( value == null && this.PollDictionary.ContainsKey(id) )
					this.PollDictionary.Remove(id);
				else if( value != null )
				{
					if( this.PollDictionary.ContainsKey(id) )
						this.PollDictionary[id] = value;
					else
						this.PollDictionary.Add(id, value);
				}
			}
		}


		public override List<Command> Init<TUser>(IBotwinderClient<TUser> client)
		{
			List<Command> commands;
			Command newCommand;

			if( !client.GlobalConfig.PollsEnabled )
			{
				commands = new List<Command>();
				newCommand = new Command("vote");
				newCommand.Type = Command.CommandType.ChatOnly;
				newCommand.Description = "Vote on a poll, created and managed by the `poll` command. Use without parameters to display the poll title and options, or vote using `vote option`.";
				newCommand.OnExecute += async (sender, e) => {
					await e.Message.Channel.SendMessageSafe("Polls are currently disabled for technical difficulties. Please be patient, we are working on it.");
				};
				commands.Add(newCommand);
				newCommand = newCommand.CreateCopy("poll");
				newCommand.Description = "Open a poll, you can then vote on it using `vote` command. Limited to one poll per server. Use without parameters to see the usage...";
				newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
				commands.Add(newCommand);
				return commands;
			}

			commands = base.Init<TUser>(client);

			this.PollDictionary = new ConcurrentDictionary<guid, Poll>();

			if( this.Data != null && this.Data.Polls != null )
			{
				foreach(Poll data in this.Data.Polls)
				{
					this.PollDictionary.Add(data.ServerID, data);
				}
			}

// !poll
			newCommand = new Command("poll");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Open a poll, you can then vote on it using `vote` command. Limited to one poll per server. Use without parameters to see the usage...";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator | Command.PermissionType.SubModerator;
			newCommand.OnExecute += async (sender, e) =>{
				string invalidParameters = string.Format("Invalid parameters. Usage:\n" +
				                                         "    `{0}{1} strict message` - Open a new **strict** poll with the `message` as the title. This will have `yes`, `no` &  `abstain` options.\n" +
				                                         "    `{0}{1} custom message` - Open a new **custom** poll with the `message` as the title. This will not have any options to begin with and you have to add them with below mentioned commands.\n" +
				                                         "    `{0}{1} free message` - Open a new **free** poll with the `message` as the title. This will not have any options, but if someone votes on anything, it will be automatically added as a new option.\n" +
				                                         "    `{0}{1} title message` - Change the title of the poll.\n" +
				                                         "    `{0}{1} add option1 \"option two\" etc` - Add custom options to your poll.\n" +
				                                         "    `{0}{1} edit oldOption newOoption` - Fix a typo maybe?\n" +
				                                         "    `{0}{1} merge oldOption newOoption` - Merge votes from the oldOption into newOption.\n" +
				                                         "    `{0}{1} results` - Display results of the current poll. This will not close it.\n" +
				                                         "    `{0}{1} close` - Close the current poll and display the results.",
					e.Server.ServerConfig.CommandCharacter, e.Command.ID
				);
				string notOpen = "This server doesn't have any polls open!";
				string alreadyOpen = string.Format("We can vote only on one poll at a time. Consider using `{0}poll close` first =]", e.Server.ServerConfig.CommandCharacter);

				string responseMessage = "Okay.";
				bool save = true;
				Regex validity1 = new Regex("(results?|close|end)");
				Regex validity2 = new Regex("(edit([oO]ption)?|merge([oO]ption)?)");

				if( e.MessageArgs == null || e.MessageArgs.Length < 1 ||
				    (e.MessageArgs.Length < 2 && !validity1.Match(e.MessageArgs[0]).Success) ||
				    (e.MessageArgs.Length < 3 && validity2.Match(e.MessageArgs[0]).Success) )
				{
					responseMessage = invalidParameters;
					save = false;
				}
				else
				{
					Poll currentPoll = ContainsKey(e.Server.ID) ? this[e.Server.ID] : null;
					switch(e.MessageArgs[0])
					{
					case "strict":
					case "custom":
					case "free":
						if( currentPoll != null )
						{
							responseMessage = alreadyOpen;
							break;
						}

						PollType type = PollType.Strict;
						if( e.MessageArgs[0] == PollType.Custom.ToString().ToLower() )
							type = PollType.Custom;
						else if( e.MessageArgs[0] == PollType.Free.ToString().ToLower() )
							type = PollType.Free;

						currentPoll = new Poll(e.Message.Server.Id, type);
						this[e.Server.ID] = currentPoll;

						if( type == PollType.Strict )
						{
							currentPoll.AddOption("yes");
							currentPoll.AddOption("no");
							currentPoll.AddOption("abstain");
						}
						goto case "title";
					case "title":
						if( currentPoll == null )
						{
							responseMessage = notOpen;
							break;
						}

						string title = "";
						for(int i = 1; i < e.MessageArgs.Length; i++)
							title += e.MessageArgs[i] + " ";

						currentPoll.Title = title.Trim();
						if( string.IsNullOrWhiteSpace(currentPoll.Title) )
							currentPoll.Title = " ";
						break;
					case "end":
					case "close":
						if( currentPoll == null )
						{
							responseMessage = notOpen;
							break;
						}

						this[e.Server.ID] = null;
						goto case "results";
					case "result":
					case "results":
						if( currentPoll == null )
						{
							responseMessage = notOpen;
							break;
						}

						Result result = currentPoll.GetResults();
						if( result.Data.Count == 0 )
						{
							responseMessage = "Nobody voted, nothing to display.";
							break;
						}

						responseMessage = currentPoll.Title + "\n```http";
						foreach(KeyValuePair<string, int> option in result.Data)
						{
							string newString = "\n" + option.Key + ": " + option.Value.ToString();
							if( newString.Length + responseMessage.Length > GlobalConfig.MessageCharacterLimit - 100 )
							{
								await e.Message.Channel.SendMessageSafe(responseMessage + "\n```");
								responseMessage = "```http";
							}
							responseMessage += newString;
						}
						responseMessage += "\n```";
						break;
					case "addoption":
					case "addoptions":
					case "addOption":
					case "addOptions":
					case "add":
						if( currentPoll == null )
						{
							responseMessage = notOpen;
							break;
						}

						for(int i = 1; i < e.MessageArgs.Length; i++)
							currentPoll.AddOption(e.MessageArgs[i]);
						break;
					case "editoption":
					case "mergeoption":
					case "editOption":
					case "mergeOption":
					case "edit":
					case "merge":
						if( currentPoll == null )
						{
							responseMessage = notOpen;
							break;
						}

						currentPoll.EditOption(e.MessageArgs[1], e.MessageArgs[2]);
						break;
					default:
						responseMessage = invalidParameters;
						break;
					}
				}

				if( save )
					SaveAsync();
				await e.Message.Channel.SendMessageSafe(responseMessage);
			};
			commands.Add(newCommand);

// !vote
			newCommand = new Command("vote");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Vote on a poll, created and managed by the `poll` command. Use without parameters to display the poll title and options, or vote using `vote option`.";
			newCommand.OnExecute += async (sender, e) =>{
				Poll currentPoll = ContainsKey(e.Server.ID) ? this[e.Server.ID] : null;
				if( currentPoll == null )
				{
					await e.Message.Channel.SendMessageSafe("There is nothing to vote on!");
					return;
				}

				if( e.MessageArgs == null || e.MessageArgs.Length < 1 )
				{
					Result result = currentPoll.GetResults();
					string responseMessage = string.Format("{0}\n_You can vote using `{1}{2} option`, {3}_", currentPoll.Title, e.Server.ServerConfig.CommandCharacter, e.Command.ID, (currentPoll.Type == PollType.Free ? ("where the option can be anything you wish." +(result.Data.Count > 0 ? " Others voted:" : "")) : "where available options are:"));

					if( result.Data.Count > 0 )
					{
						responseMessage += "\n```http";
						foreach(KeyValuePair<string, int> option in result.Data)
						{
							string newString = "\n" + option.Key + ": " + option.Value.ToString();
							if( newString.Length + responseMessage.Length > GlobalConfig.MessageCharacterLimit - 100 )
							{
								await e.Message.Channel.SendMessageSafe(responseMessage + "\n```");
								responseMessage = "```http";
							}
							responseMessage += newString;
						}
						responseMessage += "\n```";
					}

					await e.Message.Channel.SendMessageSafe(responseMessage);
				}
				else
				{
					ErrorCode result = currentPoll.AddVote(e.Message.User, e.MessageArgs[0]);
					if( result == ErrorCode.Success )
						await e.Message.Channel.SendMessageSafe(string.Format("<@{0}> has cast_ed_ a vote!", e.Message.User.Id));
					else if( result == ErrorCode.AlreadyVoted )
						await e.Message.Channel.SendMessageSafe(string.Format("Uhm, I'm afraid that I can't let you vote twice <@{0}>! ..and so I have changed your previous vote instead.", e.Message.User.Id));
					else
					{
						await e.Message.Channel.SendMessageSafe("I've no idea what are you trying to tell me... :/");
						return;
					}

					SaveAsync();
				}
			};
			commands.Add(newCommand);



			return commands;
		}

#pragma warning disable 1998
		public override async Task Update<TUser>(IBotwinderClient<TUser> client){}
#pragma warning restore 1998

		protected override void Save()
		{
			this.Data.Polls = new Poll[this.PollDictionary.Count];
			this.PollDictionary.Values.CopyTo(this.Data.Polls, 0);

			base.Save();
		}
	}
}
