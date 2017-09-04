namespace Botwinder.entities
{
	public enum RolePermissionLevel
	{
		None = 0,
		Public,
		Member,
		SubModerator,
		Moderator,
		Admin
	}

	public enum PermissionOverrides
	{
		Default = -1,
		Everyone = 0,
		Nobody,
		ServerOwner,
		Admins,
		Moderators,
		SubModerators,
		Members
	}

	public static class PermissionType
	{
		public const byte OwnerOnly		= 0;
		public const byte Everyone		= 1 << (int)PermissionOverrides.Everyone;
		public const byte ServerOwner 	= 1 << (int)PermissionOverrides.ServerOwner;
		public const byte Admin 		= 1 << (int)PermissionOverrides.Admins;
		public const byte Moderator 	= 1 << (int)PermissionOverrides.Moderators;
		public const byte SubModerator 	= 1 << (int)PermissionOverrides.SubModerators;
		public const byte Member 		= 1 << (int)PermissionOverrides.Members;
	}
}
