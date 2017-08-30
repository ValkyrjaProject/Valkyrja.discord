using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using guid = System.Int64;

namespace Botwinder.entities
{
	[Table("global_config")]
	public class GlobalConfig
	{
		[Key]
		[Required]
		[Column("configuration_name", TypeName = "varchar(255)")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public string ConfigName{ get; set; } = "default";

		[Column("discord_token", TypeName = "varchar(255)")]
		public string DiscordToken{ get; set; } = "";

		[Column("userid")]
		public guid UserId{ get; set; } = 0;

		[Column("enforce_requirements")]
		public bool EnforceRequirements{ get; set; } = false;

		[Column("verification_enabled")]
		public bool VerificationEnabled{ get; set; } = false;

		[Column("timers_enabled")]
		public bool TimersEnabled{ get; set; } = false;

		[Column("polls_enabled")]
		public bool PollsEnabled{ get; set; } = false;

		[Column("events_enabled")]
		public bool EventsEnabled{ get; set; } = false;

		[Column("giveaways_enabled")]
		public bool GiveawaysEnabled{ get; set; } = false;

		[Column("livestream_enabled")]
		public bool LivestreamEnabled{ get; set; } = false;

		[Column("total_shards")]
		public Int64 TotalShards{ get; set; } = 0;

		[Column("initial_update_delay")]
		public Int64 InitialUpdateDelay{ get; set; } = 0;

		[Column("command_prefix", TypeName = "varchar(255)")]
		public string CommandPrefix{ get; set; } = "!";

		[Column("main_serverid")]
		public guid MainServerId{ get; set; } = 0;

		[Column("main_channelid")]
		public guid MainChannelId{ get; set; } = 0;

		[Column("vip_members_max")]
		public Int64 VipMembersMax{ get; set; } = 0;

		[Column("antispam_clear_interval")]
		public Int64 AntispamClearInterval{ get; set; } = 0;

		[Column("antispam_permit_duration")]
		public Int64 AntispamPermitDuration{ get; set; } = 0;

		[Column("antispam_safety_limit")]
		public Int64 AntispamSafetyLimit{ get; set; } = 0;

		[Column("antispam_fastmessages_per_update")]
		public Int64 AntispamFastmessagesPerUpdate{ get; set; } = 0;

		[Column("antispam_update_interval")]
		public Int64 AntispamUpdateInterval{ get; set; } = 0;

		[Column("antispam_message_cache_size")]
		public Int64 AntispamMessageCacheSize{ get; set; } = 0;

		[Column("antispam_allowed_duplicates")]
		public Int64 AntispamAllowedDuplicates{ get; set; } = 0;

		[Column("target_fps")]
		public float TargetFps{ get; set; } = 0f;

		[Column("operations_max")]
		public Int64 OperationsMax{ get; set; } = 0;

		[Column("operations_extra")]
		public Int64 OperationsExtra{ get; set; } = 0;

		[Column("maintenance_memory_threshold")]
		public Int64 MaintenanceMemoryThreshold{ get; set; } = 0;

		[Column("maintenance_thread_threshold")]
		public Int64 MaintenanceThreadThreshold{ get; set; } = 0;

		[Column("maintenance_config_reloads_threshold")]
		public Int64 MaintenanceConfigReloadsThreshold{ get; set; } = 0;

		[Column("maintenance_operations_threshold")]
		public Int64 MaintenanceOperationsThreshold{ get; set; } = 0;

		[Column("maintenance_disconnects_threshold")]
		public Int64 MaintenanceDisconnectsThreshold{ get; set; } = 0;

		[Column("log_debug")]
		public bool LogDebug{ get; set; } = false;

		[Column("log_exceptions")]
		public bool LogExceptions{ get; set; } = true;

		[Column("log_commands")]
		public bool LogCommands{ get; set; } = true;

		[Column("log_responses")]
		public bool LogResponses{ get; set; } = true;
	}

	[Table("subscribers")]
	public class Subscriber
	{
		[Key]
		[Required]
		[Column("userid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid UserId{ get; set; } = 0;

		[Column("premium")]
		public bool IsPremium{ get; set; } = false;

		[Column("has_bonus")]
		public bool HasBonus{ get; set; } = false;
	}

	[Table("partners")]
	public class PartneredServer
	{
		[Key]
		[Required]
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId{ get; set; } = 0;

		[Column("premium")]
		public bool IsPremium{ get; set; } = false;
	}

	[Table("blacklist")]
	public class BlacklistEntry
	{
		[Key]
		[Required]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid Id{ get; set; } = 0;
	}
}
