#define UsingBotwinderSecure

using System;
using System.Collections.Generic;
using System.IO;
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

				try
				{
					await this.Bot.Connect();
					this.Bot.Events.Connected += async () => {
						try
						{
							while( this.Bot.Servers == null || this.Bot.Servers.Count < 420 )
							{
								await Task.Delay(60000);
							}
							GlobalContext globalContext = GlobalContext.Create(this.Bot.DbConfig.GetDbConnectionString());
							Botwinder.old.GlobalConfig oldGlobalConfig = Botwinder.old.GlobalConfig.Load();

							Console.WriteLine("Porting subscribers.");
							for( int i = 0; i < oldGlobalConfig.PartneredUserIDs.Length; i++ )
								try{
									if( !globalContext.Subscribers.Any(s => s.UserId == oldGlobalConfig.PartneredUserIDs[i]) )
										globalContext.Subscribers.Add(new Subscriber{UserId = oldGlobalConfig.PartneredUserIDs[i]});
								} catch(Exception ex) { Console.WriteLine(ex.Message); }

							Console.WriteLine("Porting partners.");
							for( int i = 0; i < oldGlobalConfig.PartneredServerIDs.Length; i++ )
								try{
									if( !globalContext.PartneredServers.Any(s => s.ServerId == oldGlobalConfig.PartneredServerIDs[i]) )
										globalContext.PartneredServers.Add(new PartneredServer{ServerId = oldGlobalConfig.PartneredServerIDs[i]});
								} catch(Exception ex) { Console.WriteLine(ex.Message); }

							Console.WriteLine("Saving global.");
							globalContext.SaveChanges();

							Console.WriteLine("Porting servers.");
							ServerContext dbContext = ServerContext.Create(this.Bot.DbConfig.GetDbConnectionString());
							foreach( KeyValuePair<guid, Server> pair in this.Bot.Servers )
							{
								try
								{
									Console.WriteLine("Server: " + pair.Value.Id.ToString());
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

									Console.WriteLine("Roles & channels: " + pair.Value.Id.ToString());
									for( int i = 0; i < oldConfig.ModChannelIgnore.Length; i++ )
									{
										ChannelConfig channel = dbContext.Channels.FirstOrDefault(c => c.ChannelId == oldConfig.ModChannelIgnore[i]);
										if( channel == null )
										{
											channel = new ChannelConfig{
												ServerId = pair.Value.Id,
												ChannelId = oldConfig.ModChannelIgnore[i]
											};
											dbContext.Channels.Add(channel);
										}
										channel.Ignored = true;
									}
									for( int i = 0; i < oldConfig.RoleIDsAdmin.Length; i++ )
									{
										RoleConfig role = dbContext.Roles.FirstOrDefault(r => r.RoleId == oldConfig.RoleIDsAdmin[i]);
										if( role == null )
										{
											role = new RoleConfig{
												ServerId = pair.Value.Id,
												RoleId = oldConfig.RoleIDsAdmin[i]
											};
											dbContext.Roles.Add(role);
										}
										role.PermissionLevel = RolePermissionLevel.Admin;
									}
									for( int i = 0; i < oldConfig.RoleIDsModerator.Length; i++ )
									{
										RoleConfig role = dbContext.Roles.FirstOrDefault(r => r.RoleId == oldConfig.RoleIDsModerator[i]);
										if( role == null )
										{
											role = new RoleConfig{
												ServerId = pair.Value.Id,
												RoleId = oldConfig.RoleIDsModerator[i]
											};
											dbContext.Roles.Add(role);
										}
										role.PermissionLevel = RolePermissionLevel.Moderator;
									}
									for( int i = 0; i < oldConfig.RoleIDsSubModerator.Length; i++ )
									{
										RoleConfig role = dbContext.Roles.FirstOrDefault(r => r.RoleId == oldConfig.RoleIDsSubModerator[i]);
										if( role == null )
										{
											role = new RoleConfig{
												ServerId = pair.Value.Id,
												RoleId = oldConfig.RoleIDsSubModerator[i]
											};
											dbContext.Roles.Add(role);
										}
										role.PermissionLevel = RolePermissionLevel.SubModerator;
									}
									for( int i = 0; i < oldConfig.RoleIDsMember.Length; i++ )
									{
										RoleConfig role = dbContext.Roles.FirstOrDefault(r => r.RoleId == oldConfig.RoleIDsMember[i]);
										if( role == null )
										{
											role = new RoleConfig{
												ServerId = pair.Value.Id,
												RoleId = oldConfig.RoleIDsMember[i]
											};
											dbContext.Roles.Add(role);
										}
										role.PermissionLevel = RolePermissionLevel.Member;
									}
									for( int i = 0; i < oldConfig.PublicRoleIDs.Length; i++ )
									{
										RoleConfig role = dbContext.Roles.FirstOrDefault(r => r.RoleId == oldConfig.PublicRoleIDs[i]);
										if( role == null )
										{
											role = new RoleConfig{
												ServerId = pair.Value.Id,
												RoleId = oldConfig.PublicRoleIDs[i]
											};
											dbContext.Roles.Add(role);
										}
										role.PermissionLevel = RolePermissionLevel.Public;
									}
								} catch(Exception ex) { Console.WriteLine(ex.Message); }

								Console.WriteLine("Database: " + pair.Value.Id.ToString());
								Botwinder.old.UserDatabase oldDatabase = Botwinder.old.UserDatabase.Load(Path.Combine("config", pair.Value.Id.ToString()));
								await oldDatabase.ForEach(async u => {
									try
									{
										UserData userData = new UserData{
											ServerId = pair.Value.Id,
											UserId = u.ID,
											Verified = u.Verified,
											WarningCount = u.WarningCount,
											KarmaCount = u.KarmaCount
										};
										if( u.Bans != null && u.Bans.Length > 0 )
											userData.BannedUntil = u.Bans[0].BannedUntil.DateTime;

										dbContext.UserDatabase.Add(userData);
									} catch(Exception ex) { Console.WriteLine(ex.Message); }
								});
							}

							Console.WriteLine("Saving servers.");
							dbContext.SaveChanges();

							Console.WriteLine("Done.");
							await Task.Delay(3000);
							globalContext.Dispose();
							dbContext.Dispose();
						}
						catch(Exception e)
						{
							Console.WriteLine(e.Message);
							Console.WriteLine(e.StackTrace);
							if( e.InnerException != null )
							{
								Console.WriteLine(e.InnerException.Message);
								Console.WriteLine(e.InnerException.StackTrace);
							}
						}
					};
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
