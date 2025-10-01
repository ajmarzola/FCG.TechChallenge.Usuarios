using APIUsuarios.Domain;
using Microsoft.AspNetCore.Identity;

namespace APIUsuarios.Infrastructure.Services
{
    public class PasswordService
    {
        private readonly PasswordHasher<User> _hasher = new();
        public string Hash(User u, string senha) => _hasher.HashPassword(u, senha);
        public bool Verify(User u, string senha) => _hasher.VerifyHashedPassword(u, u.PasswordHash, senha) != PasswordVerificationResult.Failed;
    }
}
