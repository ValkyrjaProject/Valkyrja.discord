using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using guid = System.Int64;

namespace Botwinder.entities
{
	[Table("custom_commands")]
	public class CustomCommand
	{
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId{ get; set; } = 0;

		[Column("commandid", TypeName = "varchar(255)")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public string CommandId{ get; set; } = "";

		[Column("response", TypeName = "text")]
		public string Response{ get; set; } = "This custom command was not configured.";

		[Column("description", TypeName = "text")]
		public string Description{ get; set; } = "This is custom command on this server.";

		[Column("delete_request")]
		public bool DeleteRequest{ get; set; } = false;
	}

	[Table("custom_aliases")]
	public class CustomAlias
	{
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId{ get; set; } = 0;

		[Column("commandid", TypeName = "varchar(255)")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public string CommandId{ get; set; } = "";

		[Column("alias", TypeName = "varchar(255)")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public string Alias{ get; set; } = "";
	}
}
