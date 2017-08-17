using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using guid = System.UInt64;

namespace Botwinder.Entities
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
		public UserData(Discord.User user)
		{
			AddName(user.Name);
			AddNickname(user.Nickname);
			this.ID = user.Id;
		}

		/// <summary> Returns false in case the username is already in the list. </summary>
		public bool AddName(string username)
		{
			return true; //HACK - legal

			if( this.Names == null )
			{
				this.Names = new string[1];
				this.Names[0] = username;
				return true;
			}

			if( this.Names.Contains(username) )
				return false;

			Array.Resize(ref this.Names, this.Names.Length + 1);
			this.Names[this.Names.Length - 1] = username;
			return true;
		}

		/// <summary> Returns false in case the username is already in the list. </summary>
		public bool AddNickname(string username)
		{
			return true; //HACK - legal

			if( string.IsNullOrEmpty(username) )
				return false;

			if( this.Nicknames == null )
			{
				this.Nicknames = new string[1];
				this.Nicknames[0] = username;
				return true;
			}

			if( this.Nicknames.Contains(username) )
				return false;

			Array.Resize(ref this.Nicknames, this.Nicknames.Length + 1);
			this.Nicknames[this.Nicknames.Length - 1] = username;
			return true;
		}

		public string GetNames()
		{
			return "Unable to retrieve usernames. Feature temporarily disabled."; //HACK - legal

			if( this.Names == null )
			{
				return "";
			}
			string names = "";
			for(int i=0; i< this.Names.Length; i++)
			{
				names += this.Names[i];
				if( i == this.Names.Length - 1 )
					break;

				names += ", ";
			}

			return names;
		}

		public string GetNicknames()
		{
			return "Unable to retrieve usernames. Feature temporarily disabled."; //HACK - legal

			if( this.Nicknames == null )
			{
				return "";
			}
			string names = "";
			for(int i=0; i< this.Nicknames.Length; i++)
			{
				if( string.IsNullOrEmpty(this.Nicknames[i]) )
					continue;

				names += this.Nicknames[i];
				if( i == this.Nicknames.Length - 1 )
					break;

				names += ", ";
			}

			return names;
		}

		public void AddWarning(string message)
		{
			this.WarningCount++;
			if( !string.IsNullOrEmpty(message) )
			{
				this.Notes += " | "+ message;
			}
		}
		public void RemoveAllWarnings()
		{
			if( this.WarningCount == 0 )
				return;

			this.WarningCount = 0;
			this.Notes = "";
		}
		public void RemoveLastWarning()
		{
			if( this.WarningCount == 0 )
				return;

			this.WarningCount--;

			if( this.Notes.Contains(" | ") )
			{
				int index = this.Notes.LastIndexOf(" |");
				this.Notes = this.Notes.Remove(index);
				return;
			}

			this.Notes = "";
		}

		public void AddBan(guid serverID, string serverName, DateTimeOffset bannedUntil, string warningMessage)
		{
			if( !string.IsNullOrEmpty(warningMessage) && !this.Notes.Contains(warningMessage) )
				AddWarning("Banned: "+warningMessage);

			if( this.Bans == null )
				this.Bans = new Ban[1];
			else
			{
				Ban oldBan = this.Bans.FirstOrDefault(b => b.ServerID == serverID);
				if( oldBan != null )
				{
					if( oldBan.BannedUntil < bannedUntil )
						oldBan.BannedUntil = bannedUntil;
					return;
				}

				Array.Resize(ref this.Bans, this.Bans.Length + 1);
			}

			this.Bans[this.Bans.Length - 1] = new Ban(serverID, serverName, bannedUntil);
		}
		public bool RemoveBan(guid serverID)
		{
			if( this.Bans == null || this.Bans.Length == 0 )
				return false;

			List<Ban> list = new List<Ban>(this.Bans);

			Ban banToRemove = null;
			if( (banToRemove = list.Find(b => b.ServerID == serverID)) == null )
				return false;

			list.Remove(banToRemove);
			this.Bans = list.ToArray();
			return true;
		}

		public virtual string GetWhoisString(Discord.User user = null)
		{
			string message = string.Format("<@{0}>: {1}\n    Account created at: {2}{3}\n    Known usernames: {4}\n    Known nicknames: {5}", this.ID, this.ID,
				Utils.GetTimestamp(new DateTime((long)(((this.ID/4194304)+1420070400000) * 10000 + 621355968000000000))),
				(user != null ? "\n    Joined the server: "+ Utils.GetTimestamp(user.JoinedAt) : "") +
				(user != null && user.LastOnlineAt.HasValue ? "\n    Last seen online: "+ Utils.GetTimestamp(user.LastOnlineAt.Value) : "") +
				(user != null && user.LastActivityAt.HasValue ? "\n    Last seen chatting: "+ Utils.GetTimestamp(user.LastActivityAt.Value) : ""),
				GetNames(), GetNicknames());

			if( this.Bans != null && this.Bans.Length > 0 )
			{
				message += "\n" + "They are banned on the following servers:";
				for(int i = 0; i < this.Bans.Length; i++)
				{
					message += "\n   "+ this.Bans[i].ServerName +" | Banned until: "+ (this.Bans[i].BannedUntil == DateTimeOffset.MaxValue ? "permanent" : this.Bans[i].BannedUntil.ToString());
				}
			}

			if( this.WarningCount > 0 || !string.IsNullOrEmpty(this.Notes) )
				message += "\n" + string.Format("They have {0} warnings, with these notes: {1}", this.WarningCount, this.Notes);

			if( this.Verified )
				message += "\n" + string.Format("They are verified and their contact info is: {0}", this.VerifiedInfo);

			return message;
		}
	}

	public class PermittedUser
	{
		public guid ServerId;
		public guid UserId;
		public DateTime PermittedUntil;

		public PermittedUser(guid serverID, guid userID, DateTime permittedUntil)
		{
			this.ServerId = serverID;
			this.UserId = userID;
			this.PermittedUntil = permittedUntil;
		}
	}
}
