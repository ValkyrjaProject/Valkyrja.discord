CREATE DATABASE db_botwinder;
USE db_botwinder;

CREATE TABLE `global_config` (
	`configuration_name` VARCHAR(255) NOT NULL UNIQUE,
	`discord_token` VARCHAR(255) NOT NULL,
	`userid` BIGINT UNSIGNED NOT NULL DEFAULT '278834060053446666',
	`admin_userid` BIGINT UNSIGNED NOT NULL DEFAULT '89805412676681728',
	`enforce_requirements` BOOLEAN NOT NULL DEFAULT '0',
	`verification_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`timers_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`polls_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`events_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`giveaways_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`livestream_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`total_shards` BIGINT NOT NULL DEFAULT '1',
	`initial_update_delay` BIGINT NOT NULL DEFAULT '1',
	`command_prefix` VARCHAR(255) NOT NULL DEFAULT '!',
	`main_serverid` BIGINT UNSIGNED NOT NULL DEFAULT '155821059960995840',
	`main_channelid` BIGINT UNSIGNED NOT NULL DEFAULT '170139120318808065',
	`vip_skip_queue` BOOLEAN NOT NULL DEFAULT '0',
	`vip_members_max` BIGINT NOT NULL DEFAULT '0',
	`vip_trial_hours` BIGINT NOT NULL DEFAULT '36',
	`vip_trial_joins` BIGINT NOT NULL DEFAULT '5',
	`antispam_clear_interval` BIGINT NOT NULL DEFAULT '10',
	`antispam_safety_limit` BIGINT NOT NULL DEFAULT '30',
	`antispam_fastmessages_per_update` BIGINT NOT NULL DEFAULT '5',
	`antispam_update_interval` BIGINT NOT NULL DEFAULT '6',
	`antispam_message_cache_size` BIGINT NOT NULL DEFAULT '6',
	`antispam_allowed_duplicates` BIGINT NOT NULL DEFAULT '2',
	`target_fps` FLOAT NOT NULL DEFAULT '0.05',
	`operations_max` BIGINT NOT NULL DEFAULT '2',
	`operations_extra` BIGINT NOT NULL DEFAULT '1',
	`maintenance_memory_threshold` BIGINT NOT NULL DEFAULT '3000',
	`maintenance_thread_threshold` BIGINT NOT NULL DEFAULT '44',
	`maintenance_operations_threshold` BIGINT NOT NULL DEFAULT '300',
	`maintenance_disconnect_threshold` BIGINT NOT NULL DEFAULT '20',
	`log_debug` BOOLEAN NOT NULL DEFAULT '0',
	`log_exceptions` BOOLEAN NOT NULL DEFAULT '1',
	`log_commands` BOOLEAN NOT NULL DEFAULT '1',
	`log_responses` BOOLEAN NOT NULL DEFAULT '1',
	PRIMARY KEY (`configuration_name`)
);

CREATE TABLE `subscribers` (
	`userid` BIGINT UNSIGNED NOT NULL UNIQUE,
	`has_bonus` BOOLEAN NOT NULL,
	`premium` BOOLEAN NOT NULL DEFAULT '0',
	PRIMARY KEY (`userid`)
);

CREATE TABLE `partners` (
	`serverid` BIGINT UNSIGNED NOT NULL UNIQUE,
	`premium` BOOLEAN NOT NULL DEFAULT '0',
	PRIMARY KEY (`serverid`)
);

CREATE TABLE `blacklist` (
	`id` BIGINT UNSIGNED NOT NULL UNIQUE,
	PRIMARY KEY (`id`)
);

CREATE TABLE `server_config` (
	`serverid` BIGINT UNSIGNED NOT NULL UNIQUE,
	`name` VARCHAR(255) NOT NULL,
	`invite_url` VARCHAR(255) NOT NULL,
	`localisation_id` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`timezone_utc_relative` BIGINT NOT NULL DEFAULT '0',
	`use_database` BOOLEAN NOT NULL DEFAULT '1',
	`ignore_bots` BOOLEAN NOT NULL DEFAULT '1',
	`ignore_everyone` BOOLEAN NOT NULL DEFAULT '1',
	`command_prefix` VARCHAR(255) NOT NULL DEFAULT '!',
	`command_prefix_alt` VARCHAR(255) NOT NULL,
	`execute_on_edit` BOOLEAN NOT NULL DEFAULT '1',
	`antispam_priority` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_invites` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_invites_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_duplicate` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_duplicate_crossserver` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_duplicate_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_mentions_max` BIGINT NOT NULL DEFAULT '0',
	`antispam_mentions_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_mute` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_mute_duration` BIGINT NOT NULL DEFAULT '5',
	`antispam_links_extended` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_extended_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_standard` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_standard_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_youtube` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_youtube_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_twitch` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_twitch_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_hitbox` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_hitbox_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_beam` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_beam_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_imgur` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_links_imgur_ban` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_tolerance` BIGINT NOT NULL DEFAULT '4',
	`antispam_ignore_members` BOOLEAN NOT NULL DEFAULT '0',
	`operator_roleid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`quickban_duration` BIGINT NOT NULL DEFAULT '0',
	`quickban_reason` TEXT NOT NULL,
	`mute_roleid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`mute_ignore_channelid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`karma_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`karma_limit_mentions` BIGINT NOT NULL DEFAULT '5',
	`karma_limit_minutes` BIGINT NOT NULL DEFAULT '30',
	`karma_limit_response` BOOLEAN NOT NULL DEFAULT '1',
	`karma_currency` VARCHAR(255) NOT NULL DEFAULT 'cookies',
	`karma_currency_singular` VARCHAR(255) NOT NULL DEFAULT 'cookies',
	`karma_consume_command` VARCHAR(255) NOT NULL DEFAULT 'nom',
	`karma_consume_verb` VARCHAR(255) NOT NULL DEFAULT 'nommed',
	`log_channelid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`mod_channelid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`log_bans` BOOLEAN NOT NULL DEFAULT '0',
	`log_promotions` BOOLEAN NOT NULL DEFAULT '0',
	`log_deletedmessages` BOOLEAN NOT NULL DEFAULT '0',
	`log_editedmessages` BOOLEAN NOT NULL DEFAULT '0',
	`activity_channelid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`log_join` BOOLEAN NOT NULL DEFAULT '0',
	`log_leave` BOOLEAN NOT NULL DEFAULT '0',
	`log_message_join` TEXT NOT NULL,
	`log_message_leave` TEXT NOT NULL,
	`log_mention_join` BOOLEAN NOT NULL DEFAULT '0',
	`log_mention_leave` BOOLEAN NOT NULL DEFAULT '0',
	`log_timestamp_join` BOOLEAN NOT NULL DEFAULT '0',
	`log_timestamp_leave` BOOLEAN NOT NULL DEFAULT '0',
	`welcome_pm` BOOLEAN NOT NULL DEFAULT '0',
	`welcome_message` TEXT NOT NULL,
	`welcome_roleid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`verify` BOOLEAN NOT NULL DEFAULT '0',
	`verify_on_welcome` BOOLEAN NOT NULL DEFAULT '0',
	`verify_roleid` BIGINT UNSIGNED NOT NULL DEFAULT '0',
	`verify_karma` BIGINT NOT NULL DEFAULT '3',
	`verify_message` TEXT NOT NULL,
	`exp_enabled` BOOLEAN NOT NULL DEFAULT '0',
	`base_exp_to_levelup` BIGINT NOT NULL,
	`exp_announce_levelup` BOOLEAN NOT NULL,
	`exp_per_message` BIGINT NOT NULL,
	`exp_per_attachment` BIGINT NOT NULL,
	`exp_cumulative_roles` BOOLEAN NOT NULL DEFAULT '0',
	PRIMARY KEY (`serverid`)
);

CREATE TABLE `roles` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`roleid` BIGINT UNSIGNED NOT NULL,
	`permission_level` BIGINT NOT NULL DEFAULT '0',
	`public_id` BIGINT NOT NULL DEFAULT '0',
	`logging_ignored` BOOLEAN NOT NULL DEFAULT '0',
	`antispam_ignored` BOOLEAN NOT NULL DEFAULT '0',
	`level` BIGINT NOT NULL DEFAULT '0',
	PRIMARY KEY (`roleid`)
);

CREATE TABLE `custom_commands` (
	`serverid` BIGINT UNSIGNED NOT NULL UNIQUE,
	`commandid` VARCHAR(255) NOT NULL,
	`response` TEXT NOT NULL,
	`description` TEXT NOT NULL,
	PRIMARY KEY (`serverid`,`commandid`)
);

CREATE TABLE `channels` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`channelid` BIGINT UNSIGNED NOT NULL UNIQUE,
	`ignored` BOOLEAN NOT NULL DEFAULT '0',
	`temporary` BOOLEAN NOT NULL DEFAULT '0',
	`muted_until` DATETIME NOT NULL,
	PRIMARY KEY (`channelid`)
);

CREATE TABLE `users` (
	`userid` BIGINT UNSIGNED NOT NULL,
	`serverid` BIGINT UNSIGNED NOT NULL,
	`verified` BOOLEAN NOT NULL DEFAULT '0',
	`karma_count` BIGINT NOT NULL DEFAULT '1',
	`warning_count` BIGINT NOT NULL DEFAULT '0',
	`notes` TEXT NOT NULL,
	`last_thanks_time` DATETIME NOT NULL,
	`banned_until` DATETIME NOT NULL,
	`muted_until` DATETIME NOT NULL,
	`ignored` BOOLEAN NOT NULL,
	`count_message` BIGINT NOT NULL DEFAULT '0',
	`count_attachments` BIGINT NOT NULL DEFAULT '0',
	`level_relative` BIGINT NOT NULL DEFAULT '0',
	`exp_relative` BIGINT NOT NULL,
	PRIMARY KEY (`userid`,`serverid`)
);

CREATE TABLE `custom_aliases` (
	`serverid` BIGINT UNSIGNED NOT NULL UNIQUE,
	`commandid` VARCHAR(255) NOT NULL,
	`alias` VARCHAR(255) NOT NULL,
	PRIMARY KEY (`serverid`,`alias`)
);

CREATE TABLE `livestream` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`channelid` BIGINT UNSIGNED NOT NULL,
	`type` TINYINT NOT NULL,
	`channel` VARCHAR(255) NOT NULL,
	`islive` BOOLEAN NOT NULL,
	PRIMARY KEY (`channelid`,`type`,`channel`)
);

CREATE TABLE `polls` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`title` TEXT NOT NULL,
	`type` TINYINT NOT NULL,
	PRIMARY KEY (`serverid`)
);

CREATE TABLE `votes` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`userid` BIGINT UNSIGNED NOT NULL,
	`voted` TEXT NOT NULL,
	PRIMARY KEY (`serverid`,`userid`)
);

CREATE TABLE `events` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`userid` BIGINT UNSIGNED NOT NULL,
	`checkedin` BOOLEAN NOT NULL,
	`score` FLOAT NOT NULL,
	PRIMARY KEY (`serverid`,`userid`)
);

CREATE TABLE `poll_options` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`option` TEXT NOT NULL,
	PRIMARY KEY (`serverid`)
);

CREATE TABLE `localisation` (
	`id` BIGINT UNSIGNED NOT NULL UNIQUE,
	`iso` VARCHAR(255) NOT NULL,
	`about` TEXT NOT NULL,
	`string1` TEXT NOT NULL,
	`string2...` TEXT NOT NULL,
	PRIMARY KEY (`id`)
);

CREATE TABLE `shards` (
	`id` BIGINT NOT NULL AUTO_INCREMENT UNIQUE,
	`taken` BOOLEAN NOT NULL DEFAULT '0',
	`connecting` BOOLEAN NOT NULL DEFAULT '0',
	`time_started` DATETIME NOT NULL,
	`memory_used` BIGINT NOT NULL DEFAULT '0',
	`threads_active` BIGINT NOT NULL DEFAULT '0',
	`server_count` BIGINT NOT NULL DEFAULT '0',
	`user_count` BIGINT NOT NULL DEFAULT '0',
	`messages_total` BIGINT NOT NULL DEFAULT '0',
	`messages_per_minute` BIGINT NOT NULL DEFAULT '0',
	`operations_ran` BIGINT NOT NULL DEFAULT '0',
	`operations_active` BIGINT NOT NULL DEFAULT '0',
	`disconnects` BIGINT NOT NULL DEFAULT '0',
	PRIMARY KEY (`id`)
);

CREATE TABLE `usernames` (
	`id` BIGINT NOT NULL AUTO_INCREMENT,
	`serverid` BIGINT UNSIGNED NOT NULL,
	`userid` BIGINT UNSIGNED NOT NULL,
	`username` VARCHAR(255) NOT NULL,
	PRIMARY KEY (`id`)
);

CREATE TABLE `nicknames` (
	`id` BIGINT NOT NULL AUTO_INCREMENT,
	`serverid` BIGINT UNSIGNED NOT NULL,
	`userid` BIGINT UNSIGNED NOT NULL,
	`nickname` VARCHAR(255) NOT NULL,
	PRIMARY KEY (`id`)
);

CREATE TABLE `timers` (
	`timerid` BIGINT UNSIGNED NOT NULL,
	`serverid` BIGINT UNSIGNED NOT NULL,
	`channelid` BIGINT UNSIGNED NOT NULL,
	`enabled` BOOLEAN NOT NULL,
	`self_command` BOOLEAN NOT NULL,
	`last_triggered` DATETIME NOT NULL,
	`start_at` DATETIME NOT NULL,
	`expire_after` DATETIME NOT NULL,
	`repeat_interval` BIGINT NOT NULL,
	PRIMARY KEY (`timerid`)
);

CREATE TABLE `timer_responses` (
	`id` BIGINT NOT NULL,
	`timerid` BIGINT UNSIGNED NOT NULL,
	`message` VARCHAR(255) NOT NULL,
	PRIMARY KEY (`id`)
);

CREATE TABLE `exceptions` (
	`id` BIGINT NOT NULL AUTO_INCREMENT,
	`shardid` BIGINT NOT NULL,
	`serverid` BIGINT UNSIGNED NOT NULL,
	`datetime` DATETIME NOT NULL,
	`message` VARCHAR(255) NOT NULL,
	`stack` TEXT NOT NULL,
	`data` VARCHAR(255) NOT NULL,
	PRIMARY KEY (`id`)
);

CREATE TABLE `logs` (
	`id` BIGINT NOT NULL AUTO_INCREMENT,
	`messageid` BIGINT UNSIGNED NOT NULL,
	`serverid` BIGINT UNSIGNED NOT NULL,
	`channelid` BIGINT UNSIGNED NOT NULL,
	`userid` BIGINT UNSIGNED NOT NULL,
	`type` TINYINT NOT NULL,
	`datetime` DATETIME NOT NULL,
	`message` TEXT NOT NULL,
	PRIMARY KEY (`id`)
);

CREATE TABLE `server_stats` (
	`shardid` BIGINT NOT NULL,
	`serverid` BIGINT UNSIGNED NOT NULL,
	`name` VARCHAR(255) NOT NULL,
	`ownerid` BIGINT UNSIGNED NOT NULL,
	`owner_name` VARCHAR(255) NOT NULL,
	`joined_first` DATETIME NOT NULL,
	`joined_last` DATETIME NOT NULL,
	`joined_count` BIGINT NOT NULL DEFAULT '0',
	`user_count` BIGINT NOT NULL DEFAULT '0',
	`vip` BOOLEAN NOT NULL DEFAULT '0',
	PRIMARY KEY (`serverid`)
);

CREATE TABLE `command_options` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`commandid` VARCHAR(255) NOT NULL,
	`permission_overrides` TINYINT NOT NULL,
	`delete_request` BOOLEAN NOT NULL,
	PRIMARY KEY (`serverid`,`commandid`)
);

CREATE TABLE `command_channel_options` (
	`serverid` BIGINT UNSIGNED NOT NULL,
	`commandid` VARCHAR(255) NOT NULL,
	`channelid` BIGINT UNSIGNED NOT NULL,
	`blacklisted` BOOLEAN NOT NULL,
	`whitelisted` BOOLEAN NOT NULL,
	PRIMARY KEY (`serverid`,`commandid`,`channelid`)
);

