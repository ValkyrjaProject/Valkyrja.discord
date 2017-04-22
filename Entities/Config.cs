using System;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class GlobalConfig
	{
		public const string DefaultFolder = "config";
		public const string Filename = "config.json";
		public const string DataFolder = "data";
		public const string BanDataFolder = "assetsBans";
		public const string BunnehDataFolder = "bunneh";
		public const guid Rhea = 89805412676681728;
		public const string RheaName = "Rhea#0321";
		public const int WhoisCommandLimit = 10;
		public const int MessageCharacterLimit = 2000;
		public const int ArchiveMessageLimit = 50000;
		public const int LargeOperationThreshold = 1000;
		public const int FileUploadTimeout = 30000;
		public const bool DisplayError500 = false;
		public const string OperationQueuedText = "This command was placed in a queue for large operations at position `{0}` and will be executed as soon as possible. Should you wish to cancel it at any time, use `!cancel {1}`\n_(Contributors do not have to wait.)_";

		public string BotToken = "";
		public guid[] UserIds = { 140307918200242176, 278834060053446666 };

		public bool RedditEnabled = true;
		public string RedditUsername = "";
		public string RedditPassword = "";
		public string RedditClientId = "";
		public string RedditClientSecret = "";
		public string RedditRedirectUri = "";

		public bool TimersEnabled = true;
		public bool PollsEnabled = true;
		public bool EventsEnabled = true;
		public bool GiveawaysEnabled = true;
		public bool LivestreamEnabled = true;

		public bool EnforceRequirements = false;
		public int TotalShards = 3;
		public int InitialUpdateDelay = 3;
		public string ServerConfigPath = "config";
		public string[] Game = {"with Railguns!", "at http://botwinder.info"};
		public Discord.GameType GameType = Discord.GameType.Default;
		public string GameUrl = "https://www.twitch.tv/rheaayase";
		public float RandomGameChangeInterval = 180;
		public guid[] OwnerIDs = { Rhea, Rhea };
		public guid MainServerID = 155821059960995840;
		public guid MainLogChannelID = 170139120318808065;
		public guid[] PartneredServerIDs = null;
		public guid[] PartneredUserIDs = null;
		public string CommandCharacter = "!";
		public int AntispamClearInterval = 10;
		public int AntispamPermitDuration = 180;
		public int AntispamSafetyLimit = 30;
		public int AntispamFastMessagesPerUpdate = 4;
		public int AntispamUpdateInterval = 5;
		public int AntispamMessageCacheSize = 6;
		public int AntispamAllowedDuplicates = 2;
		public int MentionsToRemember = 10;
		public int MentionsHistory = 10;
		public float TargetFPS = 1;
		public int MaximumConcurrentOperations = 1;
		public int ExtraSmallOperations = 1;
		public bool ContributorsIgnoreOperationsQueue = true;
		public float MaintenanceMemoryMultiplier = 1.2f;
		public float MaintenanceDisconnectThreshold = 4;
		public string AboutYourBot = "See http://botwinder.info for full list of features, where you can also configure them for your server, or invite me to your server.";
		public string[] AntispamIgnoredWords = null;

		[NonSerialized]
		public int LogType = int.MaxValue;

		public bool LogInfo{
			get{ return (this.LogType & Log.Type.Info) > 0; }
			set{
				if( !value && this.LogInfo )
					this.LogType -= Log.Type.Info;
				if( value && !this.LogInfo )
					this.LogType += Log.Type.Info;
			}
		}
		public bool LogDebug{
			get{ return (this.LogType & Log.Type.Debug) > 0; }
			set{
				if( !value && this.LogDebug )
					this.LogType -= Log.Type.Debug;
				if( value && !this.LogDebug )
					this.LogType += Log.Type.Debug;
			}
		}
		public bool LogWarning{
			get{ return (this.LogType & Log.Type.Warning) > 0; }
			set{
				if( !value && this.LogWarning )
					this.LogType -= Log.Type.Warning;
				if( value && !this.LogDebug )
					this.LogType += Log.Type.Warning;
			}
		}
		public bool LogExceptions{
			get{ return (this.LogType & Log.Type.Exceptions) > 0; }
			set{
				if( !value && this.LogExceptions )
					this.LogType -= Log.Type.Exceptions;
				if( value && !this.LogExceptions )
					this.LogType += Log.Type.Exceptions;
			}
		}
		public bool LogDeletedMessages{
			get{ return (this.LogType & Log.Type.DeletedMessages) > 0; }
			set{
				if( !value && this.LogDeletedMessages )
					this.LogType -= Log.Type.DeletedMessages;
				if( value && !this.LogDeletedMessages )
					this.LogType += Log.Type.DeletedMessages;
			}
		}
		public bool LogEditedMessages{
			get{ return (this.LogType & Log.Type.EditedMessages) > 0; }
			set{
				if( !value && this.LogEditedMessages )
					this.LogType -= Log.Type.EditedMessages;
				if( value && !this.LogEditedMessages )
					this.LogType += Log.Type.EditedMessages;
			}
		}
		public bool LogReceivedMessages{
			get{ return (this.LogType & Log.Type.ReceivedMessages) > 0; }
			set{
				if( !value && this.LogReceivedMessages )
					this.LogType -= Log.Type.ReceivedMessages;
				if( value && !this.LogReceivedMessages )
					this.LogType += Log.Type.ReceivedMessages;
			}
		}
		public bool LogExecutedCommands{
			get{ return (this.LogType & Log.Type.ExecutedCommands) > 0; }
			set{
				if( !value && this.LogExecutedCommands )
					this.LogType -= Log.Type.ExecutedCommands;
				if( value && !this.LogExecutedCommands )
					this.LogType += Log.Type.ExecutedCommands;
			}
		}


		[NonSerialized]
		public string Folder = "config";

		private GlobalConfig(){}
		public static GlobalConfig Load(string folder = DefaultFolder)
		{
			if( !Directory.Exists(folder) )
				Directory.CreateDirectory(folder);

			string path = Path.Combine(folder, Filename);

			if( !File.Exists(path) )
			{
				string json = JsonConvert.SerializeObject(new GlobalConfig(), Formatting.Indented);
				File.WriteAllText(path, json);
			}

			GlobalConfig config = JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(path));
			config.Folder = folder;
			return config;
		}

		public void Save()
		{
			string path = Path.Combine(this.Folder, Filename);
			string json = JsonConvert.SerializeObject(this, Formatting.Indented);
			File.WriteAllText(path, json);
		}
	}


	public class ServerConfig
	{
		public string Name = "";
		public guid ID;
		public bool UseDatabase{ get; set; }
		public bool UseGlobalDatabase{ get; set; }
		public bool IgnoreBots{ get; set; }
		public bool IgnoreEveryone{ get; set; }
		public string CommandCharacter{ get; set; }
		public bool ExecuteCommandsOnEditedMessages{ get; set; }

		public bool PrioritizeAntispam{ get; set; }
		public bool RemoveDiscordInvites{ get; set; }
		public bool BanDiscordInvites{ get; set; }
		public bool RemoveDuplicateMessages{ get; set; }
		public bool RemoveDuplicateCrossServerMessages{ get; set; }
		public bool BanDuplicateMessages{ get; set; }
		public long RemoveMassMentions{ get; set; }
		public bool BanMassMentions{ get; set; }
		public bool MuteFastMessages{ get; set; }
		public bool RemoveExtendedLinks{ get; set; }
		public bool BanExtendedLinks{ get; set; }
		public bool RemoveStandardLinks{ get; set; }
		public bool BanStandardLinks{ get; set; }
		public bool RemoveYoutubeLinks{ get; set; }
		public bool BanYoutubeLinks{ get; set; }
		public bool RemoveTwitchLinks{ get; set; }
		public bool BanTwitchLinks{ get; set; }
		public bool RemoveHitboxLinks{ get; set; }
		public bool BanHitboxLinks{ get; set; }
		public bool RemoveBeamLinks{ get; set; }
		public bool BanBeamLinks{ get; set; }
		public bool RemoveImgurOrGifLinks{ get; set; }
		public bool BanImgurOrGifLinks{ get; set; }
		public long SpambotBanLimit{ get; set; }
		public bool MembersIgnoreAntispam{ get; set; }

		public guid RoleIDOperator{ get; set; }
		public long QuickbanDuration{ get; set; }
		public string QuickbanReason{ get; set; }
		public long MuteDuration{ get; set; }
		public guid MuteRole{ get; set; }
		public guid MuteIgnoreChannel{ get; set; }
		public bool LogMessages{ get; set; }

		public bool KarmaEnabled{ get; set; }
		public long KarmaLimitMentions{ get; set; }
		public long KarmaLimitMinutes{ get; set; }
		public bool KarmaLimitResponse{ get; set; }
		public string KarmaCurrency{ get; set; }
		public string KarmaCurrencySingular{ get; set; }
		public string KarmaConsumeCommand{ get; set; }
		public string KarmaConsumeVerb{ get; set; }

		public guid ModChannel{ get; set; }
		public guid ModChannelBans{ get; set; }
		public bool ModChannelLogBans{ get; set; }
		public bool ModChannelLogMembers{ get; set; }
		public bool ModChannelLogDeletedMessages{ get; set; }
		public bool ModChannelLogEditedMessages{ get; set; }
		public bool ModChannelLogAntispam { get; set; }
		public guid[] ModChannelIgnore{ get; set; }
		public guid[] ModChannelIgnoreUsers{ get; set; }

		public guid UserActivityChannel{ get; set; }
		public bool UserActivityMention{ get; set; } //MentionJoined - unable to change for compatibility.
		public bool UserActivityMentionLeft{ get; set; }
		public bool UserActivityLogJoined{ get; set; }
		public bool UserActivityLogLeft { get; set; }
		public bool UserActivityLogTimestamp{ get; set; }
		public string UserActivityMessageJoined{ get; set; }
		public string UserActivityMessageLeft{ get; set; }

		public bool WelcomeMessageEnabled{ get; set; }
		public string WelcomeMessage{ get; set; }
		public guid WelcomeRoleID{ get; set; }
		public bool VerifyEnabled{ get; set; }
		public bool VerifyOnWelcome{ get; set; }
		public guid VerifyRoleID{ get; set; }
		public int VerifyKarma{ get; set; }
		public string VerifyPM{ get; set; }

		public bool RemovePromote{ get; set; }
		public bool RemoveJoin{ get; set; }

		public guid[] RoleIDsAdmin{ get; set; }
		public guid[] RoleIDsModerator{ get; set; }
		public guid[] RoleIDsSubModerator{ get; set; }
		public guid[] RoleIDsMember{ get; set; }
		public guid[] RoleIDsSecureMember{ get; set; }
		public guid[] PublicRoleIDs{ get; set; }

		public CustomCommand[] CustomCommands;
		public CommandAlias[] Aliases;

		public guid[] MutedUsers;
		public guid[] MutedChannels;

		private Object _Lock = new Object();
		private string Folder = "";
		public DateTime LastChangedTime{ get; set; }

		protected ServerConfig(){}
		protected ServerConfig(guid id, string name)
		{
			this.Name = name;
			this.ID = id;

			this.UseDatabase = true;
			this.UseGlobalDatabase = false;
			this.IgnoreBots = true;
			this.IgnoreEveryone = true;
			this.CommandCharacter = "!";
			this.ExecuteCommandsOnEditedMessages = true;

			this.PrioritizeAntispam = false;
			this.RemoveDiscordInvites = false;
			this.BanDiscordInvites = false;
			this.RemoveDuplicateMessages = false;
			this.RemoveDuplicateCrossServerMessages = false;
			this.BanDuplicateMessages = false;
			this.RemoveMassMentions = 0;
			this.BanMassMentions = false;
			this.MuteFastMessages = false;
			this.RemoveExtendedLinks = false;
			this.BanExtendedLinks = false;
			this.RemoveStandardLinks = false;
			this.BanStandardLinks = false;
			this.RemoveYoutubeLinks = false;
			this.BanYoutubeLinks = false;
			this.RemoveTwitchLinks = false;
			this.BanTwitchLinks = false;
			this.RemoveHitboxLinks = false;
			this.BanHitboxLinks = false;
			this.RemoveBeamLinks = false;
			this.BanBeamLinks = false;
			this.RemoveImgurOrGifLinks = false;
			this.BanImgurOrGifLinks = false;
			this.SpambotBanLimit = 7;
			this.MembersIgnoreAntispam = false;

			this.RoleIDOperator = 0;
			this.QuickbanDuration = 12;
			this.QuickbanReason = "";
			this.MuteDuration = 5;
			this.MuteRole = 0;
			this.MuteIgnoreChannel = 0;
			this.LogMessages = false;

			this.KarmaEnabled = false;
			this.KarmaLimitMentions = 5;
			this.KarmaLimitMinutes = 60;
			this.KarmaLimitResponse = true;
			this.KarmaCurrency = "cookies";
			this.KarmaCurrencySingular = "cookie";
			this.KarmaConsumeCommand = "nom";
			this.KarmaConsumeVerb = "nommed";

			this.ModChannelLogBans = false;
			this.ModChannelLogMembers = false;
			this.ModChannelLogDeletedMessages = false;
			this.ModChannelLogEditedMessages = false;
			this.ModChannelLogAntispam = false;

			this.UserActivityMention = false;
			this.UserActivityMentionLeft = false;
			this.UserActivityLogJoined = false;
			this.UserActivityLogLeft = false;
			this.UserActivityLogTimestamp = true;
			this.UserActivityMessageJoined = "{0} joined the server.";
			this.UserActivityMessageLeft = "{0} left.";

			this.WelcomeMessageEnabled = false;
			this.WelcomeMessage = "Hi {0}, welcome to our server!";
			this.WelcomeRoleID = 0;

			this.VerifyEnabled = false;
			this.VerifyOnWelcome = false;
			this.VerifyRoleID = 0;
			this.VerifyKarma = 3;
			this.VerifyPM = "Benefits of verifying are:\n**1.** Verified accounts can embed links and images.\n**2.** Ability to use voice activation instead of being forced to use push to talk.\n**3.** It will allow us to throw you a message off-discord if something happens, and it serves as a protection against bots, spammers and trolls.";

			this.RemovePromote = false;
			this.RemoveJoin = false;
		}

		public static ServerConfig Load(string folder, guid serverID, string serverName)
		{
			string path = Path.Combine(folder, serverID.ToString());

			if( !Directory.Exists(path) )
				Directory.CreateDirectory(path);

			path = Path.Combine(path, GlobalConfig.Filename);
			if( !File.Exists(path) )
			{
				string json = JsonConvert.SerializeObject(new ServerConfig(serverID, serverName), Formatting.Indented);
				File.WriteAllText(path, json);
			}

			ServerConfig newConfig = JsonConvert.DeserializeObject<ServerConfig>(File.ReadAllText(path));
			newConfig.Folder = folder;
			newConfig.LastChangedTime = DateTime.UtcNow;

			if( newConfig.Name != serverName )
			{
				newConfig.Name = serverName;
				string json = JsonConvert.SerializeObject(newConfig, Formatting.Indented);
				File.WriteAllText(path, json);
			}

			return newConfig;
		}

		public void SaveAsync()
		{
			Task.Run(() => Save());
		}
		private void Save()
		{
			string path = Path.Combine(this.Folder, this.ID.ToString(), GlobalConfig.Filename);
			lock(this._Lock)
			{
				string json = JsonConvert.SerializeObject(this, Formatting.Indented);
				File.WriteAllText(path, json);
				this.LastChangedTime = DateTime.UtcNow;
			}
		}

		public string GetPropertyValue(string propertyName, Discord.Server server)
		{
			if( propertyName == "CustomCommands" || propertyName == "Aliases" )
				return null;

			System.Reflection.PropertyInfo info = GetType().GetProperty(propertyName);
			if( info == null )
				return null;

			object value = info.GetValue(this);
			if( value == null )
				return null;

			string result = "";
			Discord.Channel channel = null;
			Discord.Role role = null;
			Discord.User user = null;

			if( value.GetType().IsArray )
			{
				guid[] collection = value as guid[]; //I know that there are no other arrays in this class.
				for(int i = 0; i < collection.Length; i++)
				{
					channel = server.GetChannel(collection[i]);
					role = server.GetRole(collection[i]);
					user = server.GetUser(collection[i]);
					result += (i == 0 ? "" : "\n") + (channel != null ? channel.Name : role != null ? role.Name : user != null ? user.Name : "deleted") + " | " + collection[i];
				}
			}
			else
			{
				guid id;
				result = value.ToString();

				if( guid.TryParse(result, out id) )
				{
					channel = server.GetChannel(id);
					role = server.GetRole(id);
					result = (channel != null ? channel.Name : role != null ? role.Name : "deleted") + " | " + result;
				}
			}

			return result;
		}
	}


	public class CommandGlobalConfig
	{
		[Serializable]
		public class Entry
		{
			public string ID;
			public guid[] BlacklistedRoleIDs;
			public guid[] WhitelistedRoleIDs;
			public guid[] WhitelistedUserIDs;
			public guid[] WhitelistedServerIDs;
			public guid[] BlacklistedServerIDs;
		}

		internal const string Filename = "commands.json";

		public Entry[] CommandOptions;

		private CommandGlobalConfig()
		{
		}
		public static CommandGlobalConfig Load(string folder)
		{
			if( !Directory.Exists(folder) )
				Directory.CreateDirectory(folder);

			string path = Path.Combine(folder, Filename);
			string json = "";

			if( !File.Exists(path) )
			{
				CommandGlobalConfig newConfig = new CommandGlobalConfig();
				newConfig.CommandOptions = new Entry[1];
				newConfig.CommandOptions[0] = new Entry();
				newConfig.CommandOptions[0].WhitelistedRoleIDs = new guid[1];
				newConfig.CommandOptions[0].WhitelistedUserIDs = new guid[1];
				newConfig.CommandOptions[0].ID = "eval";
				newConfig.CommandOptions[0].WhitelistedUserIDs[0] = GlobalConfig.Rhea;
				json = JsonConvert.SerializeObject(newConfig, Formatting.Indented);
				File.WriteAllText(path, json);
			}

			json = File.ReadAllText(path);
			CommandGlobalConfig config = JsonConvert.DeserializeObject<CommandGlobalConfig>(json);
			return config;
		}
	}
}
