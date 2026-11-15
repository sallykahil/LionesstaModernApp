using System.Security.Cryptography;
using System.Text;

namespace LionessstaAPI.Helpers
{
    // Hashes and verifies the admin password so the real password never has to
    // sit in configuration. Stored format:
    //   pbkdf2-sha256$<iterations>$<salt base64>$<hash base64>
    //
    // To generate a hash for a new password, run from the project folder:
    //   dotnet run -- hash-password "your-new-password"
    // then put the printed value in the Admin:PasswordHash setting
    // (Azure App Service setting name: Admin__PasswordHash).
    public static class PasswordHelper
    {
        private const string Prefix = "pbkdf2-sha256";
        private const int Iterations = 210_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string storedHash)
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Plain string comparison that takes the same time whether the values
        // match or not, so response timing can't be used to guess them.
        public static bool FixedTimeEquals(string a, string b)
        {
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(a)),
                SHA256.HashData(Encoding.UTF8.GetBytes(b)));
        }
    }
}
