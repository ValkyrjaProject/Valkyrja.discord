using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Bot
{
	public class ReportingSystem
	{
		public delegate void ForEachDelegate(Report user);
		public const string Filename = "reports.json";

		internal string Folder = "";

		private Object _Lock = new Object();
		protected List<Report> _List = new List<Report>();


		public guid[] ChannelIDsMods;

		public Report[] _Reports = null;
		public int Count{ get{ return _List.Count; } }


		public static ReportingSystem LoadOrCreate(string folder)
		{
			string path = Path.Combine(folder, Filename);

			if( !File.Exists(path) )
			{
				ReportingSystem newDatabase = new ReportingSystem();
				newDatabase.Folder = folder;
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
			_Reports = new Report[_List.Count];
			_List.CopyTo(_Reports, 0);

			if( !Directory.Exists(Folder) )
				Directory.CreateDirectory(Folder);

			string path = Path.Combine(Folder, Filename);
			string json = JsonConvert.SerializeObject(this, Formatting.Indented);

			lock(_Lock)
			{
				File.WriteAllText(path, json);
			}
		}

		protected static ReportingSystem Load(string folder)
		{
			string path = Path.Combine(folder, Filename);
			ReportingSystem newDatabase = JsonConvert.DeserializeObject<ReportingSystem>(File.ReadAllText(path));
			newDatabase.Folder = folder;

			if( newDatabase._Reports == null )
				newDatabase._List = new List<Report>();
			else
				newDatabase._List = new List<Report>(newDatabase._Reports);
			return newDatabase;
		}

		public bool TryGetValue(string id, out Report report)
		{
			report = _List.Find(r => r.ID == id);
			return report != null;
		}

		public void ForEach(ForEachDelegate del)
		{
			if( del == null )
				return;

			foreach(Report report in _List)
			{
				del(report);
			}
		}

		public void For(int startingIndex, ForEachDelegate del)
		{
			if( del == null )
				return;

			startingIndex = Math.Min(startingIndex, _List.Count);
			startingIndex = Math.Max(0, startingIndex);

			for(int i = startingIndex; i < _List.Count; i++)
			{
				del(_List[i]);
			}
		}

		public void Add(Report newReport)
		{
			_List.Add(newReport);
			SaveAsync();
		}

		public bool Remove(string reportID)
		{
			Report report = _List.Find(r => r.ID == reportID);
			if( report == null )
				return false;

			_List.Remove(report);
			SaveAsync();
			return true;
		}

		public void Clear()
		{
			_List.Clear();
			SaveAsync();
		}




		[System.Serializable]
		public class Report
		{
			public string ID = "";
			public guid AuthorID;
			public string Message = "";
			public string Timestamp = "";

			public Report(){}
			public Report(Discord.User author, string message)
			{
				int hash = (message + author.Id).GetHashCode();
				if(hash < 0) hash *= -1;
				ID = hash.ToString() + Botwinder.NET.BotwinderClient.Random.Next(byte.MaxValue).ToString();

				AuthorID = author.Id;
				Message = message;
				Timestamp = Botwinder.NET.Utils.GetTimestamp();
			}
		}
	}
}