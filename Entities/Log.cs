using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using guid = System.Int64;

namespace Botwinder.entities
{
	[Table("logs")]
	public class LogEntry
	{
		[Key]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Int64 Id{ get; set; } = 0;

		[Column("messageid")]
		public guid MessageId{ get; set; } = 0;

		[Column("serverid")]
		public guid ServerId{ get; set; } = 0;

		[Column("channelid")]
		public guid ChannelId{ get; set; } = 0;

		[Column("userid")]
		public guid UserId{ get; set; } = 0;

		[Column("type", TypeName = "tinyint")]
		public LogType Type{ get; set; } = LogType.None;

		[Column("message", TypeName = "text")]
		public string Message{ get; set; } = "";
	}

	public enum LogType
	{
		None = 0,
		Debug,
		Command,
		Response
	}

	[Table("exceptions")]
	public class ExceptionEntry
	{
		[Key]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Int64 Id{ get; set; } = 0;

		[Column("serverid")]
		public guid ServerId{ get; set; } = 0;

		[Column("message", TypeName = "varchar(255)")]
		public string Message{ get; set; } = "";

		[Column("stack", TypeName = "text")]
		public string Stack{ get; set; } = "";

		[Column("data", TypeName = "varchar(255)")]
		public string Data{ get; set; } = "";
	}
}
