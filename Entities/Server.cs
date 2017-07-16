using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public interface IServer
	{
		guid ID{ get; }
		string Name{ get; }
		GlobalConfig GlobalConfig{ get; }
		ServerConfig ServerConfig{ get; }
		CommandConfig CommandConfig{ get; }
		Discord.Server DiscordServer{ get; }
		Dictionary<string, Command> Commands{ get; }
		Dictionary<guid, int> AntispamMessageCount{ get; }
		Dictionary<guid, int> AntispamMuteCount{ get; }
		Dictionary<guid, Discord.Message[]> AntispamRecentMessages{ get; }

		bool IsAdmin(Discord.User user);
		bool IsGlobalAdmin(Discord.User user);
		bool IsModerator(Discord.User user);
		bool IsSubModerator(Discord.User user);
		Task<string> AssignRole(Discord.User user, string expression, Predicate<Discord.Role> canAssign);
		Task<string> RemoveRole(Discord.User user, string expression, Predicate<Discord.Role> canRemove);
	}



	public class Server<TUser>: IServer where TUser: UserData, new()
	{
		public guid ID{ get{ return this.DiscordServer.Id; } }
		public string Name{ get{ return this.DiscordServer.Name; } }

		public GlobalConfig GlobalConfig{ get{ return this._GlobalConfig; } }
		private GlobalConfig _GlobalConfig = null;

		/// <summary> Server specific Config file. </summary>
		public ServerConfig ServerConfig{ get{ return this._ServerConfig; } }
		private ServerConfig _ServerConfig = null;

		/// <summary> Server specific Config file. </summary>
		public CommandConfig CommandConfig{ get{ return this._CommandConfig; } }
		private CommandConfig _CommandConfig = null;

		/// <summary> Reference to the Discord.Server object. </summary>
		public Discord.Server DiscordServer{ get; set; }

		/// <summary> Commands that can be used on this server. Please use Botwinder.AddCommand to correctly add commands, and avoid directly changing this collection. </summary>
		public Dictionary<string, Command> Commands{ get; set; }

		public DateTime ClearAntispamMuteTime = DateTime.UtcNow;
		public Dictionary<guid, int> AntispamMuteCount{ get{ return this._AntispamMuteCount; } }
		private Dictionary<guid, int> _AntispamMuteCount = new Dictionary<guid, int>();

		public Dictionary<guid, int> AntispamMessageCount{ get{ return this._AntispamMessageCount; } }
		private Dictionary<guid, int> _AntispamMessageCount = new Dictionary<guid, int>();

		public Dictionary<guid, Discord.Message[]> AntispamRecentMessages{ get{ return this._AntispamRecentMessages; } }
		private Dictionary<guid, Discord.Message[]> _AntispamRecentMessages = new Dictionary<guid, Discord.Message[]>();

		public guid LastExecutedMessageID{ get; set; }

		/// <summary> UserDatabase used by this server. </summary>
		public UserDatabase<TUser> UserDatabase{ get; set; }

		/// <summary> Initializes a new instance of the <see cref="Botwinder_Discord.NET.Server"/> class. </summary>
		/// <param name="server"> Associated Discord.Server </param>
		public Server(Discord.Server server, GlobalConfig globalConfig)
		{
			this.DiscordServer = server;
			ReloadConfig(globalConfig);
		}

		/// <summary> Returns true if config or command options were reloaded. </summary>
		public bool ReloadConfig(GlobalConfig globalConfig, bool reloadDatabase = true, bool force = true)
		{
			this._GlobalConfig = globalConfig;
			ServerConfig newConfig = null;
			string path = Path.Combine(globalConfig.ServerConfigPath, this.ID.ToString(), GlobalConfig.Filename);
			if( force || File.GetLastWriteTimeUtc(path) > this._ServerConfig.LastChangedTime )
			{
				newConfig = ServerConfig.Load(globalConfig.ServerConfigPath, this.ID, this.Name);
			}
			if( newConfig != null )
				this._ServerConfig = newConfig;


			CommandConfig newCommandConfig = null;
			path = Path.Combine(globalConfig.ServerConfigPath, this.ID.ToString(), CommandConfig.Filename);
			if( force || File.GetLastWriteTimeUtc(path) > this._CommandConfig.LastChangedTime )
			{
				newCommandConfig = CommandConfig.Load(globalConfig.ServerConfigPath, this.ID);
			}
			if( newCommandConfig != null )
				this._CommandConfig = newCommandConfig;

			if( reloadDatabase && !this.ServerConfig.UseGlobalDatabase )
			{
				path = Path.Combine(globalConfig.ServerConfigPath, this.ID.ToString());
				this.UserDatabase = UserDatabase<TUser>.LoadOrCreate(path);
			}

			return newConfig != null || newCommandConfig != null;
		}

		public bool IsGlobalAdmin(Discord.User user)
		{
			if( this._GlobalConfig.OwnerIDs == null )
				return false;

			for(int i = 0; i < this._GlobalConfig.OwnerIDs.Length; i++)
			{
				if( user.Id == this._GlobalConfig.OwnerIDs[i] )
					return true;
			}

			return false;
		}

		public bool IsAdmin(Discord.User user)
		{
			if( IsGlobalAdmin(user) )
				return true;

			if( user.Server.Owner.Id == user.Id || (user.ServerPermissions.ManageServer && user.ServerPermissions.Administrator) )
				return true;

			if( user.Server.CurrentUser.Id == user.Id )
				return true;

			if( this._ServerConfig.RoleIDsAdmin == null )
				return false;

			for(int i = 0; i < this._ServerConfig.RoleIDsAdmin.Length; i++)
			{
				if( (new List<Discord.Role>(user.Roles)).Find(r => r.Id == this._ServerConfig.RoleIDsAdmin[i]) != null )
					return true;
			}

			return false;
		}

		public bool IsModerator(Discord.User user)
		{
			if( IsGlobalAdmin(user) )
				return true;

			if( this._ServerConfig.RoleIDsModerator == null )
				return false;

			for(int i = 0; i < this._ServerConfig.RoleIDsModerator.Length; i++)
			{
				if( (new List<Discord.Role>(user.Roles)).Find(r => r.Id == this._ServerConfig.RoleIDsModerator[i]) != null )
					return true;
			}

			return false;
		}

		public bool IsSubModerator(Discord.User user)
		{
			if( IsGlobalAdmin(user) )
				return true;

			if( this._ServerConfig.RoleIDsSubModerator == null )
				return false;

			for(int i = 0; i < this._ServerConfig.RoleIDsSubModerator.Length; i++)
			{
				if( (new List<Discord.Role>(user.Roles)).Find(r => r.Id == this._ServerConfig.RoleIDsSubModerator[i]) != null )
					return true;
			}

			return false;
		}

		public bool IsMember(Discord.User user)
		{
			if( this._ServerConfig.RoleIDsSecureMember != null )
			{
				for(int i = 0; i < this._ServerConfig.RoleIDsSecureMember.Length; i++)
				{
					if( user.Roles.FirstOrDefault(r => r.Id == this._ServerConfig.RoleIDsSecureMember[i]) != null )
						return true;
				}
			}

			if( this._ServerConfig.RoleIDsMember != null )
			{
				for(int i = 0; i < this._ServerConfig.RoleIDsMember.Length; i++)
				{
					if( user.Roles.FirstOrDefault(r => r.Id == this._ServerConfig.RoleIDsMember[i]) != null )
						return true;
				}
			}

			return false;
		}

		#pragma warning disable 1998
		/// <summary>
		/// Search for a Role and assign it to the User.
		/// </summary>
		/// <returns>response string, if an error occured, empty otherwise.</returns>
		/// <param name="user">Who to assign the role to.</param>
		/// <param name="expression">Search expression for the Role.</param>
		/// <param name="canAssign">Predicate whether to assign found role.</param>
		public async Task<string> AssignRole(Discord.User user, string expression, Predicate<Discord.Role> canAssign)
		{
			if( string.IsNullOrEmpty(expression) )
				return "You have to tell me what would you like me to assign.";

			Discord.Role foundRole = null;
			if( (foundRole = this.DiscordServer.Roles.FirstOrDefault(r => r.Name == expression.Trim())) == null &&
			    (foundRole = this.DiscordServer.Roles.FirstOrDefault(r => r.Name.ToLower() == expression.ToLower().Trim())) == null )
			{
				IEnumerable<Discord.Role> foundRoles = this.DiscordServer.Roles.Where(r => r.Name.ToLower().Contains(expression.ToLower()));
				if(foundRoles.Count() != 1)
					return "Please be more specific, I couldn't find a Role with that name (or I found more than one) (" + expression + ")";

				foundRole = foundRoles.ElementAt(0);
			}

			if( canAssign != null && !canAssign(foundRole) )
				return "I'm afraid that I can't assign that Role to you.";

			if( user.HasRole(foundRole) )
				return "You already have that Role :)";

			await user.AddRoles(foundRole);

			return "";
		}

		/// <summary>
		/// Search for a Role and remove it from the User.
		/// </summary>
		/// <returns><c>true</c>, if role was removed, <c>false</c> otherwise.</returns>
		/// <param name="user">Who to remove the role from.</param>
		/// <param name="expression">Search expression for the Role.</param>
		/// <param name="canRemove">Predicate whether to remove found role.</param>
		/// <param name="response">Text response.</param>
		public async Task<string> RemoveRole(Discord.User user, string expression, Predicate<Discord.Role> canRemove)
		{
			if( string.IsNullOrEmpty(expression) )
				return "You have to tell me what to remove.";

			Discord.Role foundRole = null;
			if( (foundRole = this.DiscordServer.Roles.FirstOrDefault(r => r.Name == expression.Trim())) == null &&
				(foundRole = this.DiscordServer.Roles.FirstOrDefault(r => r.Name.ToLower() == expression.ToLower().Trim())) == null )
			{
				IEnumerable<Discord.Role> foundRoles = this.DiscordServer.Roles.Where(r => r.Name.ToLower().Contains(expression.ToLower()));
				if(foundRoles.Count() != 1)
					return "Please be more specific, I couldn't find a Role with that name (or I found more than one) (" + expression + ")";

				foundRole = foundRoles.ElementAt(0);
			}

			if( canRemove != null && !canRemove(foundRole) )
				return "I'm afraid that I can't remove that Role from you.";

			if( !user.HasRole(foundRole) )
				return "I can't remove a role that you don't have! :D";

			await user.RemoveRoles(foundRole);

			return "";
		}
		#pragma warning restore 1998
	}
}
