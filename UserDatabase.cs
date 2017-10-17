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

		public UserData[] _UserData = null;

		public static UserDatabase Load(string folder)
		{
			string path = Path.Combine(folder, Filename);
			UserDatabase newDatabase = JsonConvert.DeserializeObject<UserDatabase>(File.ReadAllText(path));

			return newDatabase;
		}
	}
}
