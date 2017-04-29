using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Discord;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public static class Utils
	{
		public static Random Random{ get; set; } = new Random();

		public static string GetTimestamp()
		{
			return GetTimestamp(DateTime.UtcNow);
		}
		public static string GetTimestamp(DateTime time)
		{
			return time.ToUniversalTime().ToString("yyyy-MM-dd_HH:mm:ss") + " GMT";
		}
		public static string GetTimestamp(DateTimeOffset time)
		{
			return time.ToUniversalTime().ToString("yyyy-MM-dd_HH:mm:ss") + " GMT";
		}

		public static string GetLogMessage(string titleRed, string infoGreen, string nameGold, string idGreen, string tag1 = "", string msg1 = "", string tag2 = "", string msg2 = "")
		{
			msg1 = msg1.Replace('`', '\'');
			msg2 = msg2.Replace('`', '\'');
			string timestamp = GetTimestamp();
			int length = titleRed.Length + infoGreen.Length + nameGold.Length + idGreen.Length + msg1.Length + msg2.Length + timestamp.Length + 100;
			int messageLimit = 1500;
			while( length >= GlobalConfig.MessageCharacterLimit )
			{
				msg1 = msg1.Substring(0, Math.Min(messageLimit, msg1.Length)) + "**...**";
				if( !string.IsNullOrWhiteSpace(msg2) )
					msg2 = msg2.Substring(0, Math.Min(messageLimit, msg2.Length)) + "**...**";

				length = titleRed.Length + infoGreen.Length + nameGold.Length + idGreen.Length + msg1.Length + msg2.Length + timestamp.Length + 100;
				messageLimit -= 100;
			}

			string message = "";
			string tag = "";
			if( string.IsNullOrWhiteSpace(tag1) && !string.IsNullOrWhiteSpace(msg1) )
				message += msg1;
			else if( !string.IsNullOrWhiteSpace(tag1) && !string.IsNullOrWhiteSpace(msg1) )
			{
				tag = "<" + tag1;
				while( tag.Length < 9 )
					tag += " ";
				message += tag + "> " + msg1;
			}

			if( string.IsNullOrWhiteSpace(tag2) && !string.IsNullOrWhiteSpace(msg2) )
				message += "\n" + msg2;
			else if( !string.IsNullOrWhiteSpace(tag2) && !string.IsNullOrWhiteSpace(msg2) )
			{
				tag = "<" + tag2;
				while( tag.Length < 9 )
					tag += " ";
				message += "\n" + tag + "> " + msg2;
			}

			return string.Format("```md\n# {0}\n[{1}]({2})\n< {3} ={4}>\n{5}\n```", titleRed, timestamp, infoGreen, nameGold, idGreen, message);
		}

		public static List<TUser> GetMentionedUsersData<TUser>(CommandArguments e) where TUser: UserData, new()
		{
			List<TUser> mentionedUsers = new List<TUser>();

			if( e.Message.MentionedUsers != null && e.Message.MentionedUsers.Any() )
			{
				mentionedUsers.AddRange(e.Message.MentionedUsers.Select(user => (e.Server as Server<TUser>).UserDatabase.GetOrAddUser(user)));
			}
			else
			{
				if( e.MessageArgs != null && e.MessageArgs.Length > 0 )
				{
					foreach(string param in e.MessageArgs)
					{
						guid id;
						TUser user = null;
						if( guid.TryParse(param, out id) && (user = (e.Server as Server<TUser>).UserDatabase.GetUser(id)) != null )
							mentionedUsers.Add(user);
						else
							break;
					}
				}
			}

			return mentionedUsers;
		}

		public static List<User> GetMentionedUsers(CommandArguments e)
		{
			List<User> mentionedUsers = new List<User>(e.Message.MentionedUsers);
			if( e.Message.MentionedUsers == null || mentionedUsers.Count == 0 )
			{
				if( e.MessageArgs != null && e.MessageArgs.Length > 0 )
				{
					foreach(string param in e.MessageArgs)
					{
						guid id;
						User user = null;
						if( guid.TryParse(param.TrimStart('<', '@', '!').TrimEnd('>'), out id) && (user = e.Server.DiscordServer.GetUser(id)) != null )
							mentionedUsers.Add(user);
					}
				}
			}

			return mentionedUsers;
		}

		public static string GetUserMentions(List<guid> userIDs)
		{
			string userNames = "";
			for(int i = 0; i < userIDs.Count; i++)
				userNames += (i == 0 ? "<@" : (i == userIDs.Count-1 ? " and <@" : ", <@")) + userIDs[i].ToString() + ">";

			return userNames;
		}

		public static string GetUserNames(IEnumerable<User> users)
		{
			string userNames = "";
			int count = users.Count();
			for(int i = 0; i < count; i++)
			{
				User user = users.ElementAt(i);
				string name = (string.IsNullOrWhiteSpace(user.Nickname) ? user.Name : user.Nickname);
				userNames += (i == 0 ? "" : (i == count - 1 ? " and " : ", ")) + name;
			}

			return userNames;
		}
	}

	public static class ConcurrentDictionaryEx {
		public static bool Remove<TKey, TValue>(
			this ConcurrentDictionary<TKey, TValue> self, TKey key) {
			TValue ignored;
			return self.TryRemove(key, out ignored);
		}
		public static bool Add<TKey, TValue>(
			this ConcurrentDictionary<TKey, TValue> self, TKey key, TValue value) {
			return self.TryAdd(key, value);
		}
	}
}
