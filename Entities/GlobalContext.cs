using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using MySQL.Data.EntityFrameworkCore.Extensions;
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
		public DbSet<ExceptionEntry> Exceptions;
		public DbSet<Shard> Shards;
		//public DbSet<Localisation> Localisation;

		public GlobalContext(DbContextOptions<GlobalContext> options) : base(options)
		{
			this.GlobalConfigs = new InternalDbSet<GlobalConfig>(this);
		}

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

		public static GlobalContext Create(string connectionString)
		{
			DbContextOptionsBuilder<GlobalContext> optionsBuilder = new DbContextOptionsBuilder<GlobalContext>();
			optionsBuilder.UseMySQL(connectionString);

			GlobalContext newContext = new GlobalContext(optionsBuilder.Options);
			newContext.Database.EnsureCreated();
			return newContext;
		}
	}
}
