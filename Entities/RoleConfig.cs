using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using guid = System.Int64;

namespace Botwinder.entities
{
	[Table("roles")]
	public class RoleConfig
	{
		[Key]
		[Required]
		[Column("roleid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid RoleId{ get; set; } = 0;

		[Required]
		[Column("serverid")]
		public guid ServerId{ get; set; } = 0;

		[Column("permission_level", TypeName = "tinyint")]
		public RolePermissionLevel PermissionLevel{ get; set; } = RolePermissionLevel.None;

		[Column("public_id")]
		public Int64 PublicRoleGroupId{ get; set; } = 0;

		[Column("logging_ignored")]
		public bool LoggingIgnored{ get; set; } = false;

		[Column("antispam_ignored")]
		public bool AntispamIgnored{ get; set; } = false;

		[Column("level")]
		public Int64 ExpLevel{ get; set; } = 0;
	}
}
