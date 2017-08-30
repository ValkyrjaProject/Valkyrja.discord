using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Botwinder.entities
{
	[Table("shards")]
	public class Shard
	{
		[Key]
		[Required]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public Int64 Id{ get; set; } = 0;

		[Column("taken")]
		public bool IsTaken{ get; set; } = false;

		[Column("connecting")]
		public bool IsConnecting{ get; set; } = false;

		[Column("time_started")]
		public DateTime TimeStarted{ get; set; } = DateTime.MinValue;

		[Column("memory_used")]
		public Int64 MemoryUsed{ get; set; } = 0;

		[Column("threads_active")]
		public Int64 ThreadsActive{ get; set; } = 0;

		[Column("server_count")]
		public Int64 ServerCount{ get; set; } = 0;

		[Column("user_count")]
		public Int64 UserCount{ get; set; } = 0;

		[Column("messages_total")]
		public Int64 MessagesTotal{ get; set; } = 0;

		[Column("config_reloads")]
		public Int64 ConfigReloads{ get; set; } = 0;

		[Column("operations_ran")]
		public Int64 OperationsRan{ get; set; } = 0;

		[Column("operations_active")]
		public Int64 OperationsActive{ get; set; } = 0;

		[Column("disconnects")]
		public Int64 Disconnects{ get; set; } = 0;
	}
}
