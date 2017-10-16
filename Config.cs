using System;
using System.IO;
using Newtonsoft.Json;

using guid = System.UInt64;

namespace Botwinder.old
{
	public class ServerConfig
	{
		public string Name = "";
		public guid ID;
		public bool UseDatabase{ get; set; }
		public bool UseGlobalDatabase{ get; set; }
		public bool IgnoreBots{ get; set; }
		public bool IgnoreEveryone{ get; set; }
		public string CommandCharacter{ get; set; }
		public string AltCommandPrefix{ get; set; }
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
		public bool VerifyUseReddit{ get; set; } //Default to false for new servers.

		public bool RemovePromote{ get; set; }
		public bool RemoveJoin{ get; set; }

		public guid[] RoleIDsAdmin{ get; set; }
		public guid[] RoleIDsModerator{ get; set; }
		public guid[] RoleIDsSubModerator{ get; set; }
		public guid[] RoleIDsMember{ get; set; }
		public guid[] RoleIDsSecureMember{ get; set; }
		public guid[] PublicRoleIDs{ get; set; }

		public guid[] MutedUsers;
		public guid[] MutedChannels;
		public guid[] TemporaryChannels;

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
			this.AltCommandPrefix = "";
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
			this.VerifyPM = "**1.** Be respectful to others, do not start huge drama and arguments.\n" +
			                "**2.** Hate speech, \"doxxing,\" or leaking personal information will not be tolerated. Free speech isn't free of consequences.\n" +
			                "**3.** Sexual harassment, even slightly suggesting anything gender biased is inappropriate. Yes, suggesting that women should be in the kitchen is sexual harassment. And we are not a dating service either.\n" +
			                "**4.** Homophobic language or racial slurs are immature and you should not use them.\n" +
			                "**5.** Do not post explicitly sexual, gore or otherwise disturbing content. This includes jokes and meme that have somewhat racist background.\n" +
			                "**6.** Do not break the application (e.g. spamming the text that goes vertical, or having a name \"everyone\", etc...) and don't spam excessively (walls of emotes, etc...)\n" +
			                "**7.** Avoid sensitive topics, such as politics or religion.\n" +
			                "**8.** Respect authority, and do not troll moderators on duty. Do not impersonate Admins or Mods, or anyone else.\n" +
			                "**9.** Don't join just to advertise your stuff, it's rude. If you have something worthy, get in touch with us, and we can maybe give you a place in the news channel. This includes discord invite links, which will be automatically removed - get in touch with the Mods.\n" +
			                "**10.** Use common sense together with everything above.";
			this.VerifyUseReddit = false;

			this.RemovePromote = false;
			this.RemoveJoin = false;
		}

		public static ServerConfig Load(string folder, guid serverID, string serverName)
		{
			string path = Path.Combine(folder, serverID.ToString());

			if( !Directory.Exists(path) )
				Directory.CreateDirectory(path);

			path = Path.Combine(path, "config.json");
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
	}
}
