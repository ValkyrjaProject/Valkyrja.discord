using System;
using System.Threading.Tasks;

using guid = System.Int64;

namespace Botwinder.entities
{
	public interface IBotwinderClient<TUser> where TUser : UserData, new()
	{
		Task LogException(Exception exception, CommandArguments<TUser> args);
		Task LogException(Exception exception, string data, guid serverId = 0);
	}
}
