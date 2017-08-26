#define UsingBotwinderSecure

using System;
using Botwinder.core;
using Botwinder.entities;
using Botwinder.modules;
#if UsingBotwinderSecure
using Botwinder.secure;

#endif

namespace Botwinder.discord
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");
		}
	}
}
