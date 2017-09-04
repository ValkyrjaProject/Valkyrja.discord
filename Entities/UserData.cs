using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using guid = System.Int64;

namespace Botwinder.entities
{
	[Table("users")]
	public class UserData
	{
		[Required]
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId{ get; set; } = 0;

		[Required]
		[Column("userid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid UserId{ get; set; } = 0;

		//todo

		public List<Username> Usernames;
		public List<Nickname> Nicknames;
	}

	[Table("usernames")]
	public class Username
	{
		[Key]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Int64 Id{ get; set; } = 0;

		[Required]
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId{ get; set; } = 0;

		[Required]
		[Column("userid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid UserId{ get; set; } = 0;

		[Column("username")]
		public string Name{ get; set; } = "";

		public UserData UserData{ get; set; }
	}

	[Table("nicknames")]
	public class Nickname
	{
		[Key]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public Int64 Id{ get; set; } = 0;

		[Required]
		[Column("serverid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid ServerId{ get; set; } = 0;

		[Required]
		[Column("userid")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public guid UserId{ get; set; } = 0;

		[Column("nickname")]
		public string Name{ get; set; } = "";

		public UserData UserData{ get; set; }
	}
}
