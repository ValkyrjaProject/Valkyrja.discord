using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class UserDatabase<TUser> where TUser: UserData, new()
	{
		public delegate Task ForEachDelegate(TUser user);
		public const string Filename = "userDatabase.json";

		[NonSerialized]
		private Object _Lock = new Object();
		[NonSerialized]
		protected ConcurrentDictionary<guid, TUser> _Dictionary = new ConcurrentDictionary<guid, TUser>();


		public TUser[] _UserData = null;


		public TUser this[guid key]{ get{ return this._Dictionary[key]; } }

		public string Folder = "";

		public static UserDatabase<TUser> LoadOrCreate(string folder)
		{
			string path = Path.Combine(folder, Filename);

			if( !File.Exists(path) )
			{
				UserDatabase<TUser> newDatabase = new UserDatabase<TUser>();
				TUser newUserData = new TUser();
				newUserData.AddName("Rhea");
				newUserData.ID = GlobalConfig.Rhea;
				newUserData.KarmaCount = 3;
				newUserData.Notes = "The Cookie Admin.";

				newDatabase.Folder = folder;
				newDatabase._Dictionary.Add(newUserData.ID, newUserData);
				newDatabase.Save();
			}

			return Load(folder);
		}

		public void SaveAsync()
		{
			Task.Run(() => Save());
		}
		private void Save()
		{
			this._UserData = new TUser[this._Dictionary.Count];
			this._Dictionary.Values.CopyTo(this._UserData, 0);

			if( !Directory.Exists(this.Folder) )
				Directory.CreateDirectory(this.Folder);

			string path = Path.Combine(this.Folder, Filename);
			string json = JsonConvert.SerializeObject(this, Formatting.Indented);

			lock(this._Lock)
			{
				File.WriteAllText(path, json);
			}
		}

		protected static UserDatabase<TUser> Load(string folder)
		{
			string path = Path.Combine(folder, Filename);
			UserDatabase<TUser> newDatabase = JsonConvert.DeserializeObject<UserDatabase<TUser>>(File.ReadAllText(path));
			newDatabase.Folder = folder;

			newDatabase._Dictionary = new ConcurrentDictionary<guid, TUser>();
			foreach(TUser userData in newDatabase._UserData)
			{
				newDatabase._Dictionary.Add(userData.ID, userData);
			}
			return newDatabase;
		}

		public bool Contains(guid id)
		{
			return this._Dictionary.ContainsKey(id);
		}

		public bool TryGetValue(guid id, out TUser userData)
		{
			userData = null;
			return this._Dictionary.TryGetValue(id, out userData);
		}

		public List<TUser> FindAll(string expression)
		{
			string[] keywords = expression.ToLower().Split(' ');
			List<TUser> foundData = this._Dictionary.Values.ToList();
			for(int i = 0; i < keywords.Length; i++)
			{
				foundData = foundData.FindAll(d => d.GetNames().ToLower().Contains(keywords[i]) || d.GetNicknames().ToLower().Contains(keywords[i]));
			}

			return foundData;
		}

		public TUser GetUser(guid id)
		{
			if( !this._Dictionary.ContainsKey(id) )
				return null;

			return this._Dictionary[id];
		}

		public TUser GetOrAddUser(Discord.User user)
		{
			if( this._Dictionary.ContainsKey(user.Id) )
				return this._Dictionary[user.Id];

			TUser userData = new TUser();
			userData.AddName(user.Name);
			userData.AddNickname(user.Nickname);
			userData.ID = user.Id;
			this._Dictionary.Add(user.Id, userData);
			return userData;
		}

		public void AddUsername(Discord.User user)
		{
			TUser userData = GetOrAddUser(user);
			bool nicknameAdded = userData.AddNickname(user.Nickname);
			if( userData.AddName(user.Name) || nicknameAdded )
				SaveAsync();
		}

		public void AddWarning(Discord.User user, string message)
		{
			GetOrAddUser(user).AddWarning(message);
			SaveAsync();
		}
		public void RemoveLastWarning(Discord.User user)
		{
			GetOrAddUser(user).RemoveLastWarning();
			SaveAsync();
		}
		public void RemoveAllWarnings(Discord.User user)
		{
			TUser userData = GetOrAddUser(user);
			while( userData.WarningCount > 0 )
				userData.RemoveLastWarning();

			SaveAsync();
		}

		public void AddBan(guid userID, guid serverID, string serverName, DateTimeOffset bannedUntil, string warningMessage)
		{
			if( !this._Dictionary.ContainsKey(userID) )
			{
				TUser userData = new TUser();
				userData.ID = userID;
				this._Dictionary.Add(userID, userData);
			}

			this._Dictionary[userID].AddBan(serverID, serverName, bannedUntil, warningMessage);
			SaveAsync();
		}
		public void AddBan(Discord.User user, guid serverID, string serverName, DateTimeOffset bannedUntil, string warningMessage)
		{
			GetOrAddUser(user).AddBan(serverID, serverName, bannedUntil, warningMessage);
			SaveAsync();
		}
		public void RemoveBan(guid userID, guid serverID)
		{
			if( this._Dictionary.ContainsKey(userID) && this._Dictionary[userID].RemoveBan(serverID)  )
				SaveAsync();
		}

		public async Task ForEach(ForEachDelegate del)
		{
			if( del == null )
				return;

			foreach(KeyValuePair<guid, TUser> currentUser in this._Dictionary)
			{
				await del(currentUser.Value);
			}
		}
	}
}
