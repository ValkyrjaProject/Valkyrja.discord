using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Botwinder.entities
{
	[Table("localisation")]
	public class Localisation
	{
		[Key]
		[Required]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public Int64 Id{ get; set; } = 0;

		[Column("iso", TypeName = "varchar(255)")]
		public string Iso{ get; set; } = "";

		[Column("string1", TypeName = "text")]
		public string String1{ get; set; } = "";

	}
}
