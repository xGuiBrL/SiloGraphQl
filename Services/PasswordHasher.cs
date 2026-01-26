using System;

namespace InventarioSilo.Services
{
    public class PasswordHasher
    {
        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("La contraseña no puede estar vacía.", nameof(password));
            }

            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue))
            {
                return false;
            }

            if (LooksHashed(storedValue))
            {
                return BCrypt.Net.BCrypt.Verify(password, storedValue);
            }

            return string.Equals(password, storedValue, StringComparison.Ordinal);
        }

        public bool LooksHashed(string? candidate)
            => !string.IsNullOrWhiteSpace(candidate)
               && candidate.StartsWith("$2", StringComparison.Ordinal);
    }
}
