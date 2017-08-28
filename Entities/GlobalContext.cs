using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using guid = System.Int64;

namespace Botwinder.entities
{
	public class GlobalContext: DbContext
	{
		public DbSet<GlobalConfig> GlobalConfigs;
		public DbSet<Subscriber> Subscribers;
		public DbSet<PartneredServer> PartneredServers;
		public DbSet<BlacklistEntry> Blacklist;
		public DbSet<LogEntry> Log;
		public DbSet<Exception> Exceptions;

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<GlobalConfig>()
				.Property(p => p.EnforceRequirements)
				.HasDefaultValue(false);

			modelBuilder.Entity<Subscriber>()
				.Property(p => p.IsPremium)
				.HasDefaultValue(false);

			modelBuilder.Entity<PartneredServer>()
				.Property(p => p.IsPremium)
				.HasDefaultValue(false);
		}
	}
}
