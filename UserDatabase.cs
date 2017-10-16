using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.old
{
	public class UserDatabase
	{
		public delegate Task ForEachDelegate(UserData user);
		public const string Filename = "userDatabase.json";

		[NonSerialized]
		private Object _Lock = new Object();
		[NonSerialized]
		protected ConcurrentDictionary<guid, UserData> _Dictionary = new ConcurrentDictionary<guid, UserData>();


		public UserData[] _UserData = null;


		public UserData this[guid key]{ get{ return this._Dictionary[key]; } }

		public string Folder = "";

		public static UserDatabase Load(string folder)
		{
			string path = Path.Combine(folder, Filename);
			if( File.Exists(path) )
				return null;

			UserDatabase newDatabase = JsonConvert.DeserializeObject<UserDatabase>(File.ReadAllText(path));
			newDatabase.Folder = folder;

			newDatabase._Dictionary = new ConcurrentDictionary<guid, UserData>();
			foreach(UserData userData in newDatabase._UserData)
			{
				newDatabase._Dictionary.TryAdd(userData.ID, userData);
			}
			return newDatabase;
		}

		public async Task ForEach(ForEachDelegate del)
		{
			if( del == null )
				return;

			foreach(KeyValuePair<guid, UserData> currentUser in this._Dictionary)
			{
				await del(currentUser.Value);
			}
		}
	}
}
