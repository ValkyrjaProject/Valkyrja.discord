using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class CommandOptions
	{
		public string CommandId = "";
		public CommandConfig.PermissionOverrides PermissionOverrides = CommandConfig.PermissionOverrides.Default;
		public bool DeleteRequest = false;
		public guid[] ChannelBlacklist = null;
		public guid[] ChannelWhitelist = null;

		public CommandOptions(string id)
		{
			this.CommandId = id;
		}
	}

	public class CommandConfig
	{
		public enum PermissionOverrides
		{
			Default = -1,
			Everyone = 0,
			Nobody,
			ServerOwner,
			Admins,
			Moderators,
			SubModerators,
			Members
		}

		private guid Id = 0;
		public Dictionary<string, CommandOptions> CommandOptions = new Dictionary<string, CommandOptions>();


		private Object _Lock = new Object();
		private string Folder = "";
		internal const string Filename = "commands.json";
		internal DateTime LastChangedTime;

		protected CommandConfig(){}
		protected CommandConfig(guid id)
		{
			this.Id = id;
		}

		public static CommandConfig Load(string folder, guid serverID)
		{
			string path = Path.Combine(folder, serverID.ToString());

			if( !Directory.Exists(path) )
				Directory.CreateDirectory(path);

			path = Path.Combine(path, Filename);
			if( !File.Exists(path) )
			{
				string json = JsonConvert.SerializeObject(new Dictionary<string, CommandOptions>(), Formatting.Indented);
				File.WriteAllText(path, json);
			}

			CommandConfig newConfig = new CommandConfig(serverID);
			newConfig.CommandOptions = JsonConvert.DeserializeObject<Dictionary<string, CommandOptions>>(File.ReadAllText(path));
			newConfig.Folder = folder;
			newConfig.LastChangedTime = DateTime.UtcNow;

			return newConfig;
		}

		public void SaveAsync()
		{
			Task.Run(() => Save());
		}
		private void Save()
		{
			string path = Path.Combine(this.Folder, this.Id.ToString(), Filename);
			lock(this._Lock)
			{
				string json = JsonConvert.SerializeObject(this.CommandOptions, Formatting.Indented);
				File.WriteAllText(path, json);
				this.LastChangedTime = DateTime.UtcNow;
			}
		}

		public CommandOptions GetOrAddCommandOptions(string id)
		{
			CommandOptions commandOptions = null;
			if( !this.CommandOptions.ContainsKey(id) )
			{
				commandOptions = new CommandOptions(id);
				this.CommandOptions.Add(id, commandOptions);
			}
			else
			{
				commandOptions = this.CommandOptions[id];
			}

			return commandOptions;
		}
	}
}
