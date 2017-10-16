#define UsingBotwinderSecure

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Botwinder.modules;
#if UsingBotwinderSecure
using Botwinder.secure;
#endif

using guid = System.UInt64;

namespace Botwinder.discord
{
	class Program
	{
		static void Main(string[] args)
		{
			(new Client()).RunAndWait().GetAwaiter().GetResult();
		}
	}

	class Client
	{
		private BotwinderClient Bot;


		public Client()
		{}

		public async Task RunAndWait()
		{
			while( true )
			{
				this.Bot = new BotwinderClient();
				//InitModules();
				this.Bot.Events.Connected += async () => {
					await Task.Delay(600000);
					ServerContext dbContext = ServerContext.Create(this.Bot.DbConfig.GetDbConnectionString());
					foreach( KeyValuePair<guid, Server> pair in this.Bot.Servers )
					{
						Botwinder.old.ServerConfig oldConfig = Botwinder.old.ServerConfig.Load("config", pair.Value.Id, pair.Value.Guild.Name);
						ServerConfig newConfig = dbContext.ServerConfigurations.FirstOrDefault(s => s.ServerId == pair.Value.Id);
						if( newConfig == null || oldConfig == null )
							continue;

						newConfig.IgnoreBots = oldConfig.IgnoreBots;
						newConfig.IgnoreEveryone = oldConfig.IgnoreEveryone;
						newConfig.CommandPrefix = oldConfig.CommandCharacter;
						newConfig.CommandPrefixAlt = oldConfig.AltCommandPrefix;
						newConfig.ExecuteOnEdit = oldConfig.ExecuteCommandsOnEditedMessages;
						newConfig.AntispamPriority = oldConfig.PrioritizeAntispam;
						newConfig.AntispamInvites = oldConfig.RemoveDiscordInvites;
						newConfig.AntispamInvitesBan = oldConfig.BanDiscordInvites;
						newConfig.AntispamDuplicate = oldConfig.RemoveDuplicateMessages;
						newConfig.AntispamDuplicateCrossserver = oldConfig.RemoveDuplicateCrossServerMessages;
						newConfig.AntispamDuplicateBan = oldConfig.BanDuplicateMessages;
						newConfig.AntispamMentionsMax = oldConfig.RemoveMassMentions;
						newConfig.AntispamMentionsBan = oldConfig.BanMassMentions;
						newConfig.AntispamMute = oldConfig.MuteFastMessages;
						newConfig.AntispamMuteDuration = oldConfig.MuteDuration;
						newConfig.AntispamLinksExtended = oldConfig.RemoveExtendedLinks;
						newConfig.AntispamLinksExtendedBan = oldConfig.BanExtendedLinks;
						newConfig.AntispamLinksStandard = oldConfig.RemoveStandardLinks;
						newConfig.AntispamLinksStandardBan = oldConfig.BanStandardLinks;
						newConfig.AntispamLinksYoutube = oldConfig.RemoveYoutubeLinks;
						newConfig.AntispamLinksYoutubeBan = oldConfig.BanYoutubeLinks;
						newConfig.AntispamLinksTwitch = oldConfig.RemoveTwitchLinks;
						newConfig.AntispamLinksTwitchBan = oldConfig.BanTwitchLinks;
						newConfig.AntispamLinksHitbox = oldConfig.RemoveHitboxLinks;
						newConfig.AntispamLinksHitboxBan = oldConfig.BanHitboxLinks;
						newConfig.AntispamLinksBeam = oldConfig.RemoveBeamLinks;
						newConfig.AntispamLinksBeamBan = oldConfig.BanBeamLinks;
						newConfig.AntispamLinksImgur = oldConfig.RemoveImgurOrGifLinks;
						newConfig.AntispamLinksImgurBan = oldConfig.BanImgurOrGifLinks;
						newConfig.AntispamTolerance = oldConfig.SpambotBanLimit;
						newConfig.AntispamIgnoreMembers = oldConfig.MembersIgnoreAntispam;
						newConfig.OperatorRoleId = oldConfig.RoleIDOperator;
						newConfig.QuickbanDuration = oldConfig.QuickbanDuration;
						newConfig.QuickbanReason = oldConfig.QuickbanReason;
						newConfig.MuteRoleId = oldConfig.MuteRole;
						newConfig.MuteIgnoreChannelId = oldConfig.MuteIgnoreChannel;
						newConfig.KarmaEnabled = oldConfig.KarmaEnabled;
						newConfig.KarmaLimitMentions = oldConfig.KarmaLimitMentions;
						newConfig.KarmaLimitMinutes = oldConfig.KarmaLimitMinutes;
						newConfig.KarmaLimitResponse = oldConfig.KarmaLimitResponse;
						newConfig.KarmaCurrency = oldConfig.KarmaCurrency;
						newConfig.KarmaCurrencySingular = oldConfig.KarmaCurrencySingular;
						newConfig.KarmaConsumeCommand = oldConfig.KarmaConsumeCommand;
						newConfig.KarmaConsumeVerb = oldConfig.KarmaConsumeVerb;
						newConfig.LogChannelId = oldConfig.ModChannel;
						newConfig.ModChannelId = oldConfig.ModChannelBans;
						newConfig.LogBans = oldConfig.ModChannelLogBans;
						newConfig.LogPromotions = oldConfig.ModChannelLogMembers;
						newConfig.LogDeletedmessages = oldConfig.ModChannelLogDeletedMessages;
						newConfig.LogEditedmessages = oldConfig.ModChannelLogEditedMessages;
						newConfig.ActivityChannelId = oldConfig.UserActivityChannel;
						newConfig.LogJoin = oldConfig.UserActivityLogJoined;
						newConfig.LogLeave = oldConfig.UserActivityLogLeft;
						newConfig.LogMessageJoin = oldConfig.UserActivityMessageJoined;
						newConfig.LogMessageLeave = oldConfig.UserActivityMessageLeft;
						newConfig.LogMentionJoin = oldConfig.UserActivityMention;
						newConfig.LogMentionLeave = oldConfig.UserActivityMentionLeft;
						newConfig.WelcomeMessageEnabled = oldConfig.WelcomeMessageEnabled;
						newConfig.WelcomeMessage = oldConfig.WelcomeMessage;
						newConfig.WelcomeRoleId = oldConfig.WelcomeRoleID;
						newConfig.VerificationEnabled = oldConfig.VerifyEnabled;
						newConfig.VerifyOnWelcome = oldConfig.VerifyOnWelcome;
						newConfig.VerifyRoleId = oldConfig.VerifyRoleID;
						newConfig.VerifyKarma = oldConfig.VerifyKarma;
						newConfig.VerifyMessage = oldConfig.VerifyPM;
					}
				};

				try
				{
					await this.Bot.Connect();
					await Task.Delay(-1);
				}
				catch(Exception e)
				{
					await this.Bot.LogException(e, "--BotwinderClient crashed.");
					this.Bot.Dispose();
				}
			}
		}

		private void InitModules()
		{
			this.Bot.Modules.Add(new Antispam());
			this.Bot.Modules.Add(new Moderation());
			this.Bot.Modules.Add(new Verification());
		}
	}
}
