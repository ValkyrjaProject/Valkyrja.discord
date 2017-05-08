using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public interface IBotwinderClient<TUser> where TUser: UserData, new()
	{
		DateTime TimeStarted{ get; set; }
		GlobalConfig GlobalConfig{ get; set; }
		DiscordClient[] Clients{ get; set; }
		ConcurrentDictionary<guid, Server<TUser>> Servers{ get; set; }
		Object ServersLock{ get; set; }
		List<Operation> CurrentOperations{ get; set; }
		int TotalOperationsSinceStart{ get; set; }
		Object OperationsLock{ get; set; }

		ConcurrentDictionary<guid, List<guid>> ClearedMessageIDs{ get; set; }


		Server GetServer(guid id);
		Server<TUser> GetServerData(guid id);

		bool IsGlobalAdmin(User user);
		bool IsContributor(User user);

		void LogException(Exception exception, CommandArguments e = null, string context = null);
		void UserBanned(User user, Server discordServer, DateTimeOffset timeUntil, string reason = "Unknown", bool kickedOnly = false, User bannedBy = null);
		Task Ban(guid userID, Server<TUser> server, long banDurationHours, string reason, bool silent, bool deleteMessages, User bannedBy = null);
		Task UnBan(guid userID, Server<TUser> server);
		Task<bool> MuteUser(Server<TUser> server, User user, User mutedBy = null, bool unmuteAfterDelay = false);
		Task<bool> UnmuteUser(Server<TUser> server, User user, User unmutedBy = null, bool dontChangeConfig = false);
		Task Ping(Message message, Server<TUser> server);
	}
}
