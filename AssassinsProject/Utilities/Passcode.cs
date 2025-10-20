using System.Security.Cryptography;
using System.Text;

namespace AssassinsProject.Utilities;

public static class Passcode
{
    public static string Generate(int bytes = 5)
    {
        Span<byte> buf = stackalloc byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Base32NoPadding(buf.ToArray());
    }

    public static (byte[] hash, byte[] salt, string algo, int cost) Hash(string passcode, int iterations = 100_000)
    {
        var salt = Random(16);
        var hash = new Rfc2898DeriveBytes(passcode, salt, iterations, HashAlgorithmName.SHA256).GetBytes(32);
        return (hash, salt, "PBKDF2-SHA256", iterations);
    }

    public static bool Verify(string passcode, byte[] salt, byte[] expectedHash, int iterations)
    {
        var cand = new Rfc2898DeriveBytes(passcode, salt, iterations, HashAlgorithmName.SHA256).GetBytes(expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(cand, expectedHash);
    }

    private static byte[] Random(int len) { var b = new byte[len]; RandomNumberGenerator.Fill(b); return b; }

    private static string Base32NoPadding(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder((int)Math.Ceiling(data.Length / 5d) * 8);
        int current = 0, bits = 0;
        foreach (var b in data)
        {
            current = (current << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(alphabet[(current >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(alphabet[(current << (5 - bits)) & 31]);
        return sb.ToString();
    }
}
