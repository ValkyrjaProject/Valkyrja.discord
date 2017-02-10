using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class Timers
	{
		public class Timer
		{
			public guid TimerID;
			public bool Enabled = true;
			public bool IsSelfCommand = false;
			public string[] Messages;
			public DateTime LastTriggeredTime = DateTime.MinValue;
			public DateTime StartAt = DateTime.MinValue;
			public TimeSpan RepeatInterval = TimeSpan.Zero;

			public Timer(){}
			public Timer(guid id)
			{
				this.TimerID = id;
			}

			public void AddMessage(string msg)
			{
				if( this.Messages == null )
					this.Messages = new string[1];
				else
					Array.Resize(ref this.Messages, this.Messages.Length +1);

				this.Messages[this.Messages.Length -1] = msg;
			}
		}
		public class DiscordTimer: Timer
		{
			public guid ServerID;
			public guid ChannelID;

			public DiscordTimer(guid id): base(id)
			{}
		}


		public const string Filename = "timers.json";

		public DiscordTimer[] DiscordTimers;


		[NonSerialized]
		private Object _Lock = new Object();
		[NonSerialized]
		public string Folder = "";

		protected Timers(){}

		public static Timers Load(string folder)
		{
			string path = Path.Combine(folder, Filename);

			if( !Directory.Exists(folder) )
				Directory.CreateDirectory(folder);

			if( !File.Exists(path) )
			{
				string json = JsonConvert.SerializeObject(new Timers(), Formatting.Indented);
				File.WriteAllText(path, json);
			}

			Timers newConfig = JsonConvert.DeserializeObject<Timers>(File.ReadAllText(path));
			newConfig.Folder = folder;

			return newConfig;
		}

		public void SaveAsync()
		{
			Task.Run(() => Save());
		}
		private void Save()
		{
			string path = Path.Combine(this.Folder, Filename);
			lock(this._Lock)
			{
				string json = JsonConvert.SerializeObject(this, Formatting.Indented);
				File.WriteAllText(path, json);
			}
		}

		public void AddTimer(DiscordTimer timer)
		{
			if( this.DiscordTimers == null )
				this.DiscordTimers = new DiscordTimer[1];
			else
				Array.Resize(ref this.DiscordTimers, this.DiscordTimers.Length +1);
			this.DiscordTimers[this.DiscordTimers.Length - 1] = timer;
		}

		public bool RemoveTimer(guid id)
		{
			DiscordTimer timer = null;
			if( this.DiscordTimers == null || (timer = this.DiscordTimers.FirstOrDefault(t => t.TimerID == id)) == null )
				return false;
			RemoveTimer(timer);
			return true;
		}
		public void RemoveTimer(DiscordTimer timer)
		{
			if( this.DiscordTimers == null )
				return;
			List<DiscordTimer> list = this.DiscordTimers.ToList();
			list.Remove(timer);
			this.DiscordTimers = list.ToArray();
			SaveAsync();
		}

		public void SetTimerEnabled(guid id, bool enabled)
		{
			DiscordTimer timer = null;;
			if( this.DiscordTimers == null || (timer = this.DiscordTimers.FirstOrDefault(t => t.TimerID == id)) == null )
				return;

			timer.Enabled = enabled;
		}
	}
}
