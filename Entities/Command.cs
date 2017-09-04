using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using guid = System.Int64;

namespace Botwinder.entities
{
	public class Command<TUser> where TUser: UserData, new()
	{
		/// <summary> ID of a command is what you use in combination with the command character to execute it. </summary>
		public string Id{ get; set; } = "";

		/// <summary> List of aliases to this command. </summary>
		public List<string> Aliases{ get; set; }

		/// <summary> Parent of this command, if it is an alias. </summary>
		public string ParentId{ get; set; }

		/// <summary> Send the "typing" event before executing the command? </summary>
		public bool SendTyping{ get; set; } = true;

		/// <summary> Delete the message that issued the command? </summary>
		public bool DeleteRequest{ get; set; }

		/// <summary> True if this command is only alias, and not the original. </summary>
		public bool IsAlias{ get; internal set; }

		/// <summary> True if this command is hidden from the list of commands. </summary>
		public bool IsHidden{ get; set; }

		/// <summary> True if this command can not be changed by PermissionOverrides </summary>
		public bool IsCoreCommand{ get; set; }

		/// <summary> True if this is a custom command. </summary>
		public bool IsCustomCommand{ get; set; }

		/// <summary> Subscriber bonus only </summary>
		public bool IsBonusCommand{ get; set; } //todo - Check for user being partnered and bonus.

		/// <summary> Subscriber bonus only </summary>
		public bool IsPremiumCommand{ get; set; } //todo - Check for user being partnered and premium.

		/// <summary> Subscriber bonus only </summary>
		public bool IsPremiumServerwideCommand{ get; set; } //todo - Check for server being partnered and premium, and owner being partnered and premium.

		/// <summary> Use <c>Command.PermissionType</c> to determine who can use this command. Defaults to ServerOwnder + Whitelisted or Everyone </summary>
		public int RequiredPermissions = PermissionType.Everyone | PermissionType.ServerOwner;

		/// <summary> Description of this command will be used when the user invokes `help` command. </summary>
		public string Description{ get; set; } = "";


		public Func<CommandArguments<TUser>, Task> OnExecute{ get; set; }

		public async Task<bool> Execute(CommandArguments<TUser> e)
		{
			if( this.OnExecute == null )
				return false;

			if( (this.DeleteRequest || (e.CommandOptions != null && e.CommandOptions.DeleteRequest)) && e.Server.DiscordServer.CurrentUser.ServerPermissions.ManageMessages )
			{
				try
				{
					await e.Message.Delete();
				} catch(Exception)
				{}
			}

			try
			{
				if( this.SendTyping )
					await e.Message.Channel.SendIsTyping();
				await this.OnExecute(e);
			} catch(Exception exception)
			{
				await e.Client.LogException(exception, e);
			}
			return true;
		}

		/// <summary> Initializes a new instance of the <see cref="Botwinder.entities.Command"/> class. </summary>
		/// <param name="id"> You will execute the command by using CommandCharacter+Command.ID </param>
		public Command(string id)
		{
			this.Id = id;
		}

		/// <summary> Creates an alias to the Command and returns it as new command. This is runtime alias, which means that it will not affect all the servers. </summary>
		public Command<TUser> CreateRuntimeAlias(string alias)
		{
			Command<TUser> newCommand = CreateCopy(alias);
			newCommand.IsAlias = true;
			newCommand.ParentId = this.Id;

			return newCommand;
		}

		/// <summary> Creates an alias to the Command and returns it as new command. </summary>
		public Command<TUser> CreateAlias(string alias)
		{
			Command<TUser> newCommand = CreateRuntimeAlias(alias);

			if( this.Aliases == null )
				this.Aliases = new List<string>();

			this.Aliases.Add(alias);

			return newCommand;
		}

		/// <summary> Creates a copy of the Command and returns it as new command. This does not copy Aliases. </summary>
		public Command<TUser> CreateCopy(string newID)
		{
			if( this.IsAlias )
				throw new Exception("Command.CreateCopy: Trying to create a copy of an alias.");

			Command<TUser> newCommand = new Command<TUser>(newID);
			newCommand.Id = newID;
			newCommand.SendTyping = this.SendTyping;
			newCommand.DeleteRequest = this.DeleteRequest;
			newCommand.IsHidden = this.IsHidden;
			newCommand.IsCoreCommand = this.IsCoreCommand;
			newCommand.IsCustomCommand = this.IsCustomCommand;
			newCommand.IsBonusCommand = this.IsBonusCommand;
			newCommand.IsPremiumCommand = this.IsPremiumCommand;
			newCommand.RequiredPermissions = this.RequiredPermissions;
			newCommand.Description = this.Description;
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
			if( this.RequiredPermissions != PermissionType.OwnerOnly && channel != null &&
			    commandOptions != null && commandOptions.ChannelBlacklist != null &&
			    commandOptions.ChannelBlacklist.Contains(channel.Id) )
				return false;

			if( this.RequiredPermissions != PermissionType.OwnerOnly && channel != null &&
			    commandOptions != null && commandOptions.ChannelWhitelist != null &&
			    !commandOptions.ChannelWhitelist.Contains(channel.Id) )
				return false;

			//Custom Command Permission Overrides
			int requiredPermissions = this.RequiredPermissions;
			if( this.RequiredPermissions != PermissionType.OwnerOnly && commandOptions != null && commandOptions.PermissionOverrides != CommandConfig.PermissionOverrides.Default )
			{
				switch(commandOptions.PermissionOverrides)
				{
				case PermissionOverrides.Nobody:
					return false;
				case PermissionOverrides.ServerOwner:
					requiredPermissions = PermissionType.ServerOwner;
					break;
				case PermissionOverrides.Admins:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin;
					break;
				case PermissionOverrides.Moderators:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator;
					break;
				case PermissionOverrides.SubModerators:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator;
					break;
				case PermissionOverrides.Members:
					requiredPermissions = PermissionType.ServerOwner | PermissionType.Admin | PermissionType.Moderator | PermissionType.SubModerator | PermissionType.Member;
					break;
				case PermissionOverrides.Everyone:
					requiredPermissions = PermissionType.Everyone | PermissionType.ServerOwner;
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

			if( canExecute || ((requiredPermissions & PermissionType.Everyone) > 0 && !this.UserWhitelistEnabled && !this.RoleWhitelistEnabled) || (this.UserWhitelistEnabled && this.UserWhitelist.Contains(user.Id)) )
				return true;

			return false;
		}
	}

	public class CommandArguments<TUser> where TUser: UserData, new() //todo ...
	{
		/// <summary> Reference to the client. </summary>
		public IBotwinderClient<TUser> Client{ get; private set; }

		/// <summary> The parrent command. </summary>
		public Command<TUser> Command{ get; private set; }

		/// <summary> Custom server-side options, can be null! </summary>
		public CommandOptions CommandOptions{ get; private set; }

		/// <summary> Custom server-side options, can be null! </summary>
		public CommandChannelOptions CommandChannelOptions{ get; private set; }

		/// <summary> Server, where this command was executed. Null for PM. </summary>
		public IServer Server{ get; private set; }

		/// <summary> Message, where the command was invoked. </summary>
		public Message Message{ get; private set; }

		/// <summary> Text of the Message, where the command was invoked. The command itself is excluded. </summary>
		public string TrimmedMessage{ get; private set; }

		/// <summary> Command parameters (individual words) from the original message. MessageArgs[0] == Command.ID; </summary>
		public string[] MessageArgs{ get; private set; }


		public CommandArguments(IBotwinderClient<TUser> client, Command<TUser> command, CommandOptions options, CommandChannelOptions channelOptions, IServer server, Message message, string trimmedMessage, string[] messageArgs)
		{
			this.Client = client;
			this.Command = command;
			this.CommandOptions = options;
			this.CommandChannelOptions = channelOptions;
			this.Server = server;
			this.Message = message;
			this.TrimmedMessage = trimmedMessage;
			this.MessageArgs = messageArgs;
		}
	}
}
