using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class Blacklist
	{
		public const string Filename = "blacklist.json";

		public guid[] ServerIDs;
		public guid[] OwnerIDs;


		[NonSerialized]
		private Object Lock = new Object();
		[NonSerialized]
		public string Folder = "";

		protected Blacklist(){}

		public static Blacklist Load(string folder)
		{
			string path = Path.Combine(folder, Filename);

			if( !Directory.Exists(folder) )
				Directory.CreateDirectory(folder);

			if( !File.Exists(path) )
			{
				string json = JsonConvert.SerializeObject(new Blacklist(), Formatting.Indented);
				File.WriteAllText(path, json);
			}

			Blacklist newConfig = JsonConvert.DeserializeObject<Blacklist>(File.ReadAllText(path));
			newConfig.Folder = folder;

			return newConfig;
		}

		private void Save()
		{
			string path = Path.Combine(this.Folder, Filename);
			string json = JsonConvert.SerializeObject(this, Formatting.Indented);
			File.WriteAllText(path, json);
		}

		public void AddOwner(guid id)
		{
			lock(this.Lock)
			{
				if( this.OwnerIDs == null )
					this.OwnerIDs = new guid[1];
				else
					Array.Resize(ref this.OwnerIDs, this.OwnerIDs.Length + 1);
				this.OwnerIDs[this.OwnerIDs.Length - 1] = id;

				Save();
			}
		}

		public void RemoveOwner(guid id)
		{
			lock(this.Lock)
			{
				if( this.OwnerIDs == null )
					return;
				List<guid> list = this.OwnerIDs.ToList();
				list.Remove(id);
				this.OwnerIDs = list.ToArray();

				Save();
			}
		}

		public void AddServer(guid id)
		{
			lock(this.Lock)
			{
				if( this.ServerIDs == null )
					this.ServerIDs = new guid[1];
				else
					Array.Resize(ref this.ServerIDs, this.ServerIDs.Length + 1);
				this.ServerIDs[this.ServerIDs.Length - 1] = id;

				Save();
			}
		}

		public void RemoveServer(guid id)
		{
			lock(this.Lock)
			{
				if( this.ServerIDs == null )
					return;
				List<guid> list = this.ServerIDs.ToList();
				list.Remove(id);
				this.ServerIDs = list.ToArray();

				Save();
			}
		}
	}
}
