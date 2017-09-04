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
		public DbSet<ServerConfig> ServerConfigurations;
		public DbSet<ChannelConfig> Channels;
		public DbSet<RoleConfig> Roles;

		public DbSet<CommandOptions> CommandOptions;
		public DbSet<CommandChannelOptions> CommandChannelOptions;
		public DbSet<CustomCommand> CustomCommands;
		public DbSet<CustomAlias> CustomAliases;

		public DbSet<UserData> UserDatabase;

		public ServerContext(DbContextOptions<GlobalContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<CommandOptions>()
				.HasKey(p => new{p.ServerId, p.CommandId});

			modelBuilder.Entity<CommandChannelOptions>()
				.HasKey(p => new{p.ServerId, p.CommandId, p.ChannelId});

			modelBuilder.Entity<CustomCommand>()
				.HasKey(p => new{p.ServerId, p.CommandId});

			modelBuilder.Entity<CustomAlias>()
				.HasKey(p => new{p.ServerId, p.Alias});

			modelBuilder.Entity<UserData>()
				.HasKey(p => new{p.ServerId, p.UserId});

			modelBuilder.Entity<Username>()
				.HasOne(p => p.UserData)
				.WithMany(p => p.Usernames)
				.HasForeignKey(p => new{p.ServerId, p.UserId});

			modelBuilder.Entity<Nickname>()
				.HasOne(p => p.UserData)
				.WithMany(p => p.Nicknames)
				.HasForeignKey(p => new{p.ServerId, p.UserId});
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
