using System;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.old
{
	public class GlobalConfig
	{
		public const string DefaultFolder = "config";
		public const string Filename = "config.json";
		public const string CommandsListFile = "commandsList.json";
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

		public bool RedditEnabled = true; //This boolean does not work because I borked it when I implemented the new generic verification system.
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
		public string GameUrl = "https://www.twitch.tv/rheaayase";
		public float RandomGameChangeInterval = 180;
		public guid[] OwnerIDs = { Rhea, Rhea };
		public guid MainServerID = 155821059960995840;
		public guid MainLogChannelID = 170139120318808065;
		public guid[] PartneredServerIDs = null;
		public guid[] PartneredUserIDs = null;
		public int VipUsercountThreshold = 20000;
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
		public int MaintenanceMemoryThreshold = 2000;
		public int MaintenanceThreadThreshold = 50;
		public int MaintenanceConfigReloadsThreshold = 100;
		public int MaintenanceOperationsThreshold = 200;
		public float MaintenanceDisconnectThreshold = 4;
		public string AboutYourBot = "See http://botwinder.info for full list of features, where you can also configure them for your server, or invite me to your server.";
		public string[] AntispamIgnoredWords = null;

		[NonSerialized]
		public string Folder = "config";

		private GlobalConfig(){}
		public static GlobalConfig Load(string folder = DefaultFolder)
		{
			string path = Path.Combine(folder, Filename);

			GlobalConfig config = JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(path));
			config.Folder = folder;
			return config;
		}
	}
}
