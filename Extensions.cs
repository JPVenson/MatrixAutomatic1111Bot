using System.Security.Cryptography;

namespace SkribeSeinSDBot;
public static class Extensions
{
	public static string Spite(string that, string orThat)
	{
		if (RandomNumberGenerator.GetBytes(1)[0] % 5 == 0)
		{
			return orThat;
		}

		return that;
	}
}