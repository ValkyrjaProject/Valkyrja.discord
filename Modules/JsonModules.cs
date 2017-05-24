using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Botwinder.Entities;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Modules
{
	public abstract class JsonModule<T>: IModule where T: new()
	{
		protected T Data{ get; set; }
		public bool UpdateInProgress{ get; set; } = false;


		protected abstract string Filename{ get; }
		protected Object Lock{ get; } = new Object();
		public string Folder{ get; protected set; }

		protected JsonModule(){}

		protected virtual void Load(string folder)
		{
			this.Folder = folder;
			string path = Path.Combine(this.Folder, this.Filename);

			if( !Directory.Exists(this.Folder) )
				Directory.CreateDirectory(this.Folder);

			if( !File.Exists(path) )
			{
				this.Data = new T();
				Save();
			}
			else
			{
				this.Data = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
			}
		}

		public void SaveAsync()
		{
			Task.Run(() => Save());
		}
		protected virtual void Save()
		{
			string path = Path.Combine(this.Folder, this.Filename);

			if( !Directory.Exists(this.Folder) )
				Directory.CreateDirectory(this.Folder);

			lock(this.Lock)
			{
				string json = JsonConvert.SerializeObject(this.Data, Formatting.Indented);
				File.WriteAllText(path, json);
			}
		}

#pragma warning disable 1998, 67
		public virtual List<Command> Init<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new()
		{
			Load(client.GlobalConfig.Folder);
			return new List<Command>();
		}
		public virtual async Task Update<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new(){}
		public event EventHandler<ModuleExceptionArgs> HandleException;

		public virtual void TriggerException(object sender, ModuleExceptionArgs args)
		{
			if( HandleException != null )
				HandleException(sender, args);
			else
				throw args.Exception;
		}

#pragma warning restore 1998, 67
	}
}
