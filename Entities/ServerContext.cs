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
	public class ServerContext: DbContext
	{
		public DbSet<CommandOptions> CommandOptions;
		public DbSet<CommandOptions> CommandChannelOptions;
		public DbSet<CustomCommand> CustomCommands;
		public DbSet<CustomAlias> CustomAliases;

		public ServerContext(DbContextOptions<GlobalContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<CommandOptions>()
				.HasKey(p => new { p.ServerId, p.CommandId });

			modelBuilder.Entity<CommandChannelOptions>()
				.HasKey(p => new{p.ServerId, p.CommandId, p.ChannelId});

			modelBuilder.Entity<CustomCommand>()
				.HasKey(p => new { p.ServerId, p.CommandId });

			modelBuilder.Entity<CustomAlias>()
				.HasKey(p => new { p.ServerId, p.Alias });

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
