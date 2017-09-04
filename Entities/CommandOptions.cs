using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using guid = System.Int64;

namespace Botwinder.entities
{
	[Table("command_options")]
	public class CommandOptions
	{
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId = 0;

		[Column("commandid", TypeName = "varchar(255)")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public string CommandId = "";

		[Column("permission_overrides", TypeName = "tinyint")]
		public PermissionOverrides PermissionOverrides = PermissionOverrides.Default;

		[Column("delete_request")]
		public bool DeleteRequest = false;
	}

	[Table("command_channel_options")]
	public class CommandChannelOptions
	{
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId = 0;

		[Column("commandid", TypeName = "varchar(255)")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public string CommandId = "";

		[Column("channelidid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ChannelId = 0;

		[Column("blacklisted")]
		public bool Blacklisted = false;

		[Column("whitelisted")]
		public bool Whitelisted = false;
	}
}
