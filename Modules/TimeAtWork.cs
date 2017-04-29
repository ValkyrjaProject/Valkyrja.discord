using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Botwinder.Entities;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class TimeAtWork: IModule
	{
		private ConcurrentDictionary<guid, UserTimeAtWork> TimeAtWorkCache = new ConcurrentDictionary<guid, UserTimeAtWork>();


		public List<Command> Init<TUser>(IBotwinderClient<TUser> client) where TUser : UserData, new()
		{
			List<Command> commands = new List<Command>();

// !work
			Command newCommand = new Command("work");
			newCommand.Type = Command.CommandType.PmAndChat;
			newCommand.Description = "Botwinder will keep track of your time at work (uses GMT for calculations of the month)";
			newCommand.OnExecute += async (sender, e) =>{
				string invalidParameters = string.Format("Invalid parameters. Usage:\n" +
				                                         "    `{0}{1} new` - Open a new day, and start a session in it.\n" +
				                                         "    `{0}{1} start/end` - Start or end a session for current day.\n" +
				                                         "    `{0}{1} today/week/month/total` - Display number of hours you spent at work...\n",
					e.Server == null ? client.GlobalConfig.CommandCharacter : e.Server.ServerConfig.CommandCharacter, e.Command.ID
				);

				string responseMessage = "It's empty :<";

				if( !this.TimeAtWorkCache.ContainsKey(e.Message.User.Id) )
				{
					if( !Directory.Exists(UserTimeAtWork.Foldername) )
						Directory.CreateDirectory(UserTimeAtWork.Foldername);
					string path = Path.Combine(UserTimeAtWork.Foldername, e.Message.User.Id.ToString() + ".json");
					this.TimeAtWorkCache.Add(e.Message.User.Id, UserTimeAtWork.Load(path));
				}

				UserTimeAtWork timeAtWork = this.TimeAtWorkCache[e.Message.User.Id];

				if( e.MessageArgs == null || e.MessageArgs.Length < 1 )
				{
					responseMessage = invalidParameters;
				}
				else
				{
					try
					{
						switch(e.MessageArgs[0])
						{
						case "reload":
							timeAtWork = this.TimeAtWorkCache[e.Message.User.Id] = UserTimeAtWork.Load(timeAtWork.Filepath);
							responseMessage = "Done.";
							break;
						case "new":
						case "newDay":
						case "newday":
						case "startNew":
						case "startnew":
							timeAtWork.NewDay();
							timeAtWork.StartSession();
							responseMessage = "Welcome!";
							break;
						case "start":
						case "begin":
						case "join":
							if (!timeAtWork.StartedSession)
							{
								timeAtWork.StartSession();
								responseMessage = "Welcome!";
							}
							else
							{
								responseMessage = "You are already working!";
							}
							break;
						case "stop":
						case "end":
						case "leave":
							if( timeAtWork.Days != null && timeAtWork.Days.Length > 0 )
							{
								if (timeAtWork.StartedSession)
								{
									timeAtWork.StopSession();
									TimeSpan time = timeAtWork.GetLastDay();
									responseMessage = string.Format("You spent {0} hours and {1} minutes at work, see you next time =)",
										(int) time.TotalHours, time.Minutes);
								}
								else
								{
									responseMessage = "You have not started working yet!";
								}
							}
							break;
						case "now":
						case "current":
						case "last":
						case "today":
							if( timeAtWork.Days != null && timeAtWork.Days.Length > 0 )
							{
								TimeSpan time = timeAtWork.GetLastDay();
								responseMessage = string.Format("You spent {0} hours and {1} minutes at work.", (int)time.TotalHours, time.Minutes);
							}
							break;
						case "total":
							if( timeAtWork.Days != null && timeAtWork.Days.Length > 0 )
							{
								TimeSpan time = timeAtWork.GetTotal();
								responseMessage = string.Format("You spent {0} hours and {1} minutes at work since {2}", (int)time.TotalHours, time.Minutes, timeAtWork.Days[0].Date.ToShortDateString());
							}
							break;
						case "week":
							if( timeAtWork.Days != null && timeAtWork.Days.Length > 0 )
							{
								List<WorkDay> days = timeAtWork.GetDaysInLastWeek();
								TimeSpan time = TimeSpan.Zero;
								days.ForEach(d => time += d.GetTime());
								responseMessage = string.Format("You spent {0} hours and {1} minutes at work since {2}", (int)time.TotalHours, time.Minutes, days[0].Date.ToShortDateString());
								if( days.Count > 1 )
								{
									for(int i = 0; i < days.Count; i++)
									{
										time = days[i].GetTime();
										responseMessage += (i == 0 ? "\n    (days: " : ", ") + string.Format("{0}h{1}m", (int)time.TotalHours, time.Minutes) + (i == days.Count -1 ? ")" : "");
									}
								}
							}
							break;
						case "month":
							if( timeAtWork.Days != null && timeAtWork.Days.Length > 0 )
							{
								List<TimeSpan> weeks = timeAtWork.GetWeeksInLastMonth();
								responseMessage = "Not implemented yet.";
								TimeSpan time = TimeSpan.Zero;
								weeks.ForEach(w => time += w);
								responseMessage = string.Format("You spent {0} hours and {1} minutes at work last logged month", (int)time.TotalHours, time.Minutes);
								if( weeks.Count > 1 )
								{
									for(int i = 0; i < weeks.Count; i++)
									{
										responseMessage += (i == 0 ? "\n    (weeks: " : ", ") + string.Format("{0}h{1}m", (int)weeks[i].TotalHours, weeks[i].Minutes) + (i == weeks.Count -1 ? ")" : "");
									}
								}
							}
							break;
						default:
							responseMessage = "Wat?!";
							break;
						}
					} catch(Exception exception)
					{
						responseMessage += exception.Message;
					}
				}

				await e.Message.Channel.SendMessage(responseMessage);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("job"));

			return commands;
		}

#pragma warning disable 1998, 0067
		public async Task Update<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new(){}
		public event EventHandler<ModuleExceptionArgs> HandleException;
#pragma warning restore 1998, 0067


		public class UserTimeAtWork
		{
			public const string Foldername = "TimeAtWork";

			public WorkDay[] Days;


			[NonSerialized] private Object _Lock = new Object();
			[NonSerialized] public string Filepath = "";

			protected UserTimeAtWork()
			{
			}

			public static UserTimeAtWork Load(string path)
			{
				if( !File.Exists(path) )
				{
					string json = JsonConvert.SerializeObject(new UserTimeAtWork(), Formatting.Indented);
					File.WriteAllText(path, json);
				}

				UserTimeAtWork newConfig = JsonConvert.DeserializeObject<UserTimeAtWork>(File.ReadAllText(path));
				newConfig.Filepath = path;

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
					File.WriteAllText(this.Filepath, json);
				}
			}

			public void NewDay()
			{
				if( this.Days == null )
					this.Days = new WorkDay[1];
				else
					Array.Resize<WorkDay>(ref this.Days, this.Days.Length + 1);

				this.Days[this.Days.Length - 1] = new WorkDay();
			}

			public void StartSession()
			{
				if( this.Days == null || this.Days.Length == 0 )
					throw new Exception("Called TimeAtWork.StartSession() without previous NewDay();");
				this.Days[this.Days.Length - 1].Start();
				SaveAsync();
			}

			public void StopSession()
			{
				this.Days[this.Days.Length - 1].Stop();
				SaveAsync();
			}

			public bool StartedSession => this.Days != null && this.Days[this.Days.Length - 1].Started;

			public TimeSpan GetLastDay()
			{
				if( this.Days == null || this.Days.Length == 0 )
					throw new Exception("Called TimeAtWork.GetTotal() while WorkDays[] array is empty.");

				return this.Days[this.Days.Length - 1].GetTime();
			}

			public TimeSpan GetTotal()
			{
				if( this.Days == null || this.Days.Length == 0 )
					throw new Exception("Called TimeAtWork.GetTotal() while WorkDays[] array is empty.");

				TimeSpan difference = TimeSpan.Zero;
				for(int i = 0; i < this.Days.Length; i++)
				{
					difference += this.Days[i].GetTime();
				}

				return difference;
			}

			public List<WorkDay> GetDaysInLastWeek()
			{
				if( this.Days == null || this.Days.Length == 0 )
					throw new Exception("Called TimeAtWork.GetDaysInWeek() while WorkDays[] array is empty.");

				int i = this.Days.Length;
				List<WorkDay> days = new List<WorkDay>();
				do
				{
					days.Insert(0, this.Days[--i]);
				} while(i > 0 && (int)this.Days[i - 1].Date.DayOfWeek < (int)this.Days[i].Date.DayOfWeek);

				return days;
			}

			public List<TimeSpan> GetWeeksInLastMonth()
			{
				if( this.Days == null || this.Days.Length == 0 )
					throw new Exception("Called TimeAtWork.GetWeeksInMonth() while WorkDays[] array is empty.");

				int i = this.Days.Length;
				List<TimeSpan> weeks = new List<TimeSpan>();
				weeks.Add(TimeSpan.Zero);

				do
				{
					weeks[0] += this.Days[--i].GetTime();
					if( i != 0 && (int)this.Days[i - 1].Date.DayOfWeek > (int)this.Days[i].Date.DayOfWeek )
					{
						weeks.Insert(0, TimeSpan.Zero);
					}
				} while(i != 0 && this.Days[i - 1].Date.Day <= this.Days[i].Date.Day);

				return weeks;
			}
		}

		public class WorkDay
		{
			public DateTime Date;
			public WorkSession[] Sessions;

			public WorkDay()
			{
				this.Date = DateTime.UtcNow;
			}

			public void Start()
			{
				if( this.Sessions == null )
					this.Sessions = new WorkSession[1];
				else if( this.Sessions[this.Sessions.Length - 1].TimeStopped == default(DateTimeOffset) )
					throw new Exception("Called WorkDay.Start() without previous Stop();");
				else
					Array.Resize<WorkSession>(ref this.Sessions, this.Sessions.Length + 1);

				this.Sessions[this.Sessions.Length - 1] = new WorkSession(DateTimeOffset.UtcNow);
			}

			public void Stop()
			{
				if( this.Sessions == null || this.Sessions.Length == 0 || this.Sessions[this.Sessions.Length - 1].TimeStopped != default(DateTimeOffset) )
					throw new Exception("Called WorkDay.Stop() without previous Start();");

				this.Sessions[this.Sessions.Length - 1].TimeStopped = DateTimeOffset.UtcNow;
			}

			public bool Started => this.Sessions != null && this.Sessions[this.Sessions.Length - 1].TimeStopped == default(DateTimeOffset);

			public TimeSpan GetTime()
			{
				TimeSpan difference = TimeSpan.Zero;
				for(int i = 0; i < this.Sessions.Length; i++)
				{
					DateTimeOffset end = this.Sessions[i].TimeStopped;
					if( end == default(DateTimeOffset) )
					{
						if( i != this.Sessions.Length - 1 )
						{
							throw new Exception("WorkDay.GetTime: Error in the Sessions array. " + this.Date.Date.ToString());
						}

						end = DateTimeOffset.UtcNow;
					}

					difference += end - this.Sessions[i].TimeStarted;
				}

				return difference;
			}
		}

		public struct WorkSession
		{
			public DateTimeOffset TimeStarted;
			public DateTimeOffset TimeStopped;

			public WorkSession(DateTimeOffset start, DateTimeOffset end = default(DateTimeOffset))
			{
				this.TimeStarted = start;
				this.TimeStopped = end;
			}
		}
	}
}
