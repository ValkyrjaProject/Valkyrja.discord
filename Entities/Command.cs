using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class Command
	{
		public enum CommandType
		{
			PmOnly,
			ChatOnly,
			PmAndChat
		}

		public static class PermissionType
		{
			public const byte OwnerOnly = 0;
			public const byte WhitelistedOrEveryone	= 1 << (int)CommandConfig.PermissionOverrides.Everyone;
			public const byte ServerOwner 			= 1 << (int)CommandConfig.PermissionOverrides.ServerOwner;
			public const byte Admin 				= 1 << (int)CommandConfig.PermissionOverrides.Admins;
			public const byte Moderator 			= 1 << (int)CommandConfig.PermissionOverrides.Moderators;
			public const byte SubModerator 			= 1 << (int)CommandConfig.PermissionOverrides.SubModerators;
			public const byte Member 				= 1 << (int)CommandConfig.PermissionOverrides.Members;
		}

		/// <summary> ID of a command is what you use in combination with the command character to execute it. </summary>
		public string ID{ get{ return this._ID; } }
		private string _ID = "";

		/// <summary> List of aliases to this command. </summary>
		public string[] Aliases;

		/// <summary> Parent of this command, if it is an alias. </summary>
		public string ParentID;

		/// <summary> Where can the command be executed. </summary>
		public CommandType Type = CommandType.ChatOnly;

		/// <summary> Send the "typing" event before executing the command? </summary>
		public bool SendTyping = true;

		/// <summary> Delete the message that issued the command? </summary>
		public bool DeleteRequest = false;

		/// <summary> True if this command is only alias, and not the original. </summary>
		public bool IsAlias{ get; internal set; }

		/// <summary> True if this command is hidden from the list of commands. </summary>
		public bool IsHidden{ get; set; }

		/// <summary> True if this command can not be changed by `permissions` or `restrict` </summary>
		public bool IsCoreCommand{ get; set; }

		/// <summary> Set ServerWhitelistEnabled = true; to use the ServerWhitelist. Otherwise the command will be available on all the servers. </summary>
		public bool ServerWhitelistEnabled = false;

		/// <summary> IDs of Whitelisted Servers. If ServerWhitelistEnabled, the Command can only be used on Whitelisted Servers. </summary>
		public List<guid> ServerWhitelist{ get{ return this._ServerWhitelist; } }
		private List<guid> _ServerWhitelist = new List<guid>();

		/// <summary> IDs of Blacklisted Servers. The Command will not be used on Blacklisted Servers. </summary>
		public List<guid> ServerBlacklist{ get{ return this._ServerBlacklist; } }
		private List<guid> _ServerBlacklist = new List<guid>();

		/// <summary> Use <c>Command.PermissionType</c> to determine who can use this command. Defaults to ServerOwnder + Whitelisted or Everyone </summary>
		public int RequiredPermissions = PermissionType.WhitelistedOrEveryone | PermissionType.ServerOwner;

		/// <summary> Set UserWhitelistEnabled = true; to use the UserWhitelist. Otherwise the command will be to all Users (or whitelisted Roles, see RoleWhitelistEnabled) </summary>
		public bool UserWhitelistEnabled = false;

		/// <summary> IDs of Whitelisted Users - if UserWhitelistEnabled, only whitelisted Users will be allowed to use the command. </summary>
		public List<guid> UserWhitelist{ get{ return this._UserWhitelist; } }
		private List<guid> _UserWhitelist = new List<guid>();

		/// <summary>
		/// Set RoleWhitelistEnabled = true; to use the RoleWhitelist. Otherwise the command will be available to all Roles (or whitelisted Users, see UserWhitelistEnabled)
		/// </summary>
		public bool RoleWhitelistEnabled = false;

		/// <summary> IDs of Whitelisted Roles - if RoleWhitelistEnabled, only whitelisted Roles will be allowed to use the command. </summary>
		public List<guid> RoleWhitelist{ get{ return this._RoleWhitelist; } }
		private List<guid> _RoleWhitelist = new List<guid>();

		/// <summary>
		/// Set RoleBlacklistEnabled = true; to use the RoleBlacklist. Otherwise the command will be available to all Roles (or only the whitelisted roles or users)
		/// </summary>
		public bool RoleBlacklistEnabled = false;

		/// <summary> IDs of Blacklisted Roles - if RoleBlacklistEnabled, these blacklisted Roles will not be allowed to use the command. </summary>
		public List<guid> RoleBlacklist{ get{ return this._RoleBlacklist; } }
		private List<guid> _RoleBlacklist = new List<guid>();

		/// <summary> Description of this command will be used when the user invokes `help` command. </summary>
		public string Description = "";


		/// <summary> An object that can be used by the !eval command to store stuff. </summary>
		public object[] EvalObjects = null;


		public delegate Task OnExecuteDelegate(object sender, CommandArguments e);
		public OnExecuteDelegate OnExecute = null;
		public async Task<bool> Execute<TUser>(object sender, CommandArguments e) where TUser: UserData, new()
		{
			if( this.OnExecute == null )
				return false;
			try
			{
				if( (this.DeleteRequest || (e.CommandOptions != null && e.CommandOptions.DeleteRequest)) && e.Server.DiscordServer.CurrentUser.ServerPermissions.ManageMessages )
				{
					try
					{
						await e.Message.Delete();
					} catch(Exception)
					{}
				}

				if( this.SendTyping )
					await e.Message.Channel.SendIsTyping();
				await this.OnExecute(sender, e);
			} catch(Exception exception)
			{
				if( exception.GetType() != typeof(Discord.Net.HttpException) )
					(sender as IBotwinderClient<TUser>).LogException(exception, e);
			}
			return true;
		}

		/// <summary> Initializes a new instance of the <see cref="Botwinder_Discord.NET.Command"/> class. </summary>
		/// <param name="ID"> You will execute the command by using CommandCharacter+Command.ID </param>
		public Command(string id)
		{
			this._ID = id;
		}

		/// <summary> Creates an alias to the Command and returns it as new command. This is runtime alias, which means that it will not affect all the servers. </summary>
		public Command CreateRuntimeAlias(string alias)
		{
			Command newCommand = CreateCopy(alias);
			newCommand.IsAlias = true;
			newCommand.ParentID = this.ID;

			return newCommand;
		}

		/// <summary> Creates an alias to the Command and returns it as new command. </summary>
		public Command CreateAlias(string alias)
		{
			Command newCommand = CreateCopy(alias);
			newCommand.IsAlias = true;
			newCommand.ParentID = this.ID;

			if( this.Aliases == null || this.Aliases.Length == 0 )
				this.Aliases = new string[1];
			else
				Array.Resize<string>(ref this.Aliases, this.Aliases.Length+1);
			this.Aliases[this.Aliases.Length - 1] = alias;

			return newCommand;
		}

		/// <summary> Creates a copy of the Command and returns it as new command. This does not copy Aliases. </summary>
		public Command CreateCopy(string newID)
		{
			Command newCommand = new Command(newID);
			newCommand._ID = newID;
			newCommand.Type = this.Type;
			newCommand.SendTyping = this.SendTyping;
			newCommand.DeleteRequest = this.DeleteRequest;
			newCommand.IsHidden = this.IsHidden;
			newCommand.RequiredPermissions = this.RequiredPermissions;
			newCommand.Description = this.Description;
			newCommand.ServerWhitelistEnabled = this.ServerWhitelistEnabled;
			newCommand.RoleBlacklistEnabled = this.RoleBlacklistEnabled;
			newCommand.RoleWhitelistEnabled = this.RoleWhitelistEnabled;
			newCommand.UserWhitelistEnabled = this.UserWhitelistEnabled;
			newCommand._ServerWhitelist = this.ServerWhitelist;
			newCommand._RoleBlacklist = this._RoleBlacklist;
			newCommand._RoleWhitelist = this.RoleWhitelist;
			newCommand._UserWhitelist = this.UserWhitelist;
			newCommand.OnExecute = this.OnExecute;
			return newCommand;
		}

		/// <summary> Returns true if the User has permission to execute this command. </summary>
		/// <param name="server"> Server will be null if this is used in PM. </param>
		public bool CanExecute<TUser>(IBotwinderClient<TUser> client, Server<TUser> server, Channel channel, User user, CommandOptions commandOptions) where TUser: UserData, new()
		{
			// server == null, if this command is used in PM, in which case we look only at the user whitelist.
			if( client.IsGlobalAdmin(user) )
				return true;

			//This is now obsolete code and given enough time it should be removed. And I mean the whole whitelist/blacklist system.
			if( server != null && this.RoleBlacklistEnabled && this.RoleBlacklist != null )
				foreach(Role role in user.Roles)
					if( this.RoleBlacklist.Contains(role.Id) )
						return false;

			//Custom Command Channel Permissions
			if( this.RequiredPermissions != PermissionType.OwnerOnly && channel != null && commandOptions != null && commandOptions.ChannelBlacklist != null && commandOptions.ChannelBlacklist.Contains(channel.Id) )
				return false;

			//Custom Command Permission Overrides
			int requiredPermissions = this.RequiredPermissions;
			if( this.RequiredPermissions != PermissionType.OwnerOnly && commandOptions != null && commandOptions.PermissionOverrides != CommandConfig.PermissionOverrides.Default )
			{
				switch(commandOptions.PermissionOverrides)
				{
				case CommandConfig.PermissionOverrides.Nobody:
					return false;
				case CommandConfig.PermissionOverrides.ServerOwner:
					requiredPermissions = PermissionType.ServerOwner;
					break;
				case CommandConfig.PermissionOverrides.Admins:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
					break;
				case CommandConfig.PermissionOverrides.Moderators:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
					break;
				case CommandConfig.PermissionOverrides.SubModerators:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
					break;
				case CommandConfig.PermissionOverrides.Members:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator | PermissionType.Member;
					break;
				case CommandConfig.PermissionOverrides.Everyone:
					requiredPermissions = PermissionType.WhitelistedOrEveryone | PermissionType.ServerOwner;
					break;
				default:
					throw new ArgumentOutOfRangeException("permissionOverrides");
				}
			}

			//Actually check permissions.
			bool canExecute = server != null && (requiredPermissions & PermissionType.ServerOwner) > 0 && (user.Server.Owner.Id == user.Id || (user.ServerPermissions.ManageServer && user.ServerPermissions.Administrator ));
			canExecute |= server != null && (requiredPermissions & PermissionType.Admin) > 0 && server.IsAdmin(user);
			canExecute |= server != null && (requiredPermissions & PermissionType.Moderator) > 0 && server.IsModerator(user);
			canExecute |= server != null && (requiredPermissions & PermissionType.SubModerator) > 0 && server.IsSubModerator(user);
			canExecute |= server != null && (requiredPermissions & PermissionType.Member) > 0 && server.IsMember(user);

			if( canExecute || ((requiredPermissions & PermissionType.WhitelistedOrEveryone) > 0 && !this.UserWhitelistEnabled && !this.RoleWhitelistEnabled) || (this.UserWhitelistEnabled && this.UserWhitelist.Contains(user.Id)) )
				return true;

			if( server != null && this.RoleWhitelistEnabled && this.RoleWhitelist != null )
				for(int i = 0; i < this.RoleWhitelist.Count; i++)
					if( user.Roles.FirstOrDefault(r => r.Id == this.RoleWhitelist[i]) != null )
						return true;

			return false;
		}
	}

	public class CommandArguments
	{
		/// <summary> True if the commands was executed in the Private Message and False if it was text channel. </summary>
		public bool IsPm{ get; private set; }

		/// <summary> The parrent command. </summary>
		public Command Command{ get; private set; }

		/// <summary> Custom server-side options, can be null! </summary>
		public CommandOptions CommandOptions{ get; private set; }

		/// <summary> Server, where this command was executed. Null for PM. </summary>
		public IServer Server{ get; private set; }

		/// <summary> Message, where the command was invoked. </summary>
		public Message Message{ get; private set; }

		/// <summary> Text of the Message, where the command was invoked. The command itself is excluded. </summary>
		public string TrimmedMessage{ get; private set; }

		/// <summary> Command parameters (individual words) from the original message. MessageArgs[0] == Command.ID; </summary>
		public string[] MessageArgs{ get; private set; }


		public CommandArguments(Command command, CommandOptions options, IServer server, Message message, string trimmedMessage, string[] messageArgs)
		{
			this.Command = command;
			this.CommandOptions = options;
			this.Server = server;
			this.Message = message;
			this.TrimmedMessage = trimmedMessage;
			this.MessageArgs = messageArgs;
			this.IsPm = message.Channel.IsPrivate;
		}
	}


	public class CustomCommand
	{
		public string ID = "";
		public string Description = "This is custom command on this server.";
		public string Response = "This custom command was not configured.";
		public bool DeleteRequest = false;
		public guid[] RoleWhitelist;

		CustomCommand(){}
		public CustomCommand(string id)
		{
			this.ID = id;
		}
	}

	public class CommandAlias
	{
		public string CommandID = "";
		public string Alias = "";

		public CommandAlias(string commandID, string alias)
		{
			this.CommandID = commandID;
			this.Alias = alias;
		}
	}
}
