using System;

using guid = System.UInt64;

namespace Botwinder.old
{
	[Serializable]
	public class UserData
	{
		[Serializable]
		public class Ban
		{
			public guid ServerID;
			public string ServerName;
			public DateTimeOffset BannedUntil;

			public Ban(guid serverID, string serverName, DateTimeOffset bannedUntil)
			{
				this.ServerID = serverID;
				this.ServerName = serverName;
				this.BannedUntil = bannedUntil;
			}
		}

		public string[] Names = null;
		public string[] Nicknames = null;
		public guid ID;
		public string VerifiedInfo = "";
		public bool Verified = false;
		public int KarmaCount = 1;
		public int WarningCount = 0;
		public string Notes = "";
		public DateTimeOffset LastThanksTimestamp;
		public Ban[] Bans;

		public UserData(){}
	}
}
