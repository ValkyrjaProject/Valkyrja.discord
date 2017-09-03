using System;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace Botwinder.entities
{
	public class DbConfig
	{
		public const string Filename = "dbConfig.json";
		private Object _Lock = new Object();

		public string Host = "127.0.0.1";
		public string Port = "3306";
		public string Username = "db_user";
		public string Password = "db_password";
		public string Database = "db_botwinder";
		public string ConfigName = "default";

		public string GetDbConnectionString()
		{
			return $"server={this.Host};userid={this.Username};pwd={this.Password};port={this.Port};database={this.Database};sslmode=none;";
		}

		public static DbConfig Load()
		{
			if( !File.Exists(Filename) )
			{
				string json = JsonConvert.SerializeObject(new DbConfig(), Formatting.Indented);
				File.WriteAllText(Filename, json);
			}

			DbConfig newConfig = JsonConvert.DeserializeObject<DbConfig>(File.ReadAllText(Filename));

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
				File.WriteAllText(Filename, json);
			}
		}
	}
}
