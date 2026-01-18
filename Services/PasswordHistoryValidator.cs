using GRP_03_27.Data;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Identity;

namespace GRP_03_27.Services
{
    public class PasswordHistoryValidator<TUser> : IPasswordValidator<TUser> where TUser : User
    {
        private readonly ApplicationDbContext _context;
        private readonly int _passwordHistoryLimit = 5; // Remember last 5 passwords

        public PasswordHistoryValidator(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string password)
        {
            var previousPasswords = _context.PasswordHistories
                .Where(ph => ph.UserId == user.Id)
                .OrderByDescending(ph => ph.CreatedDate)
                .Take(_passwordHistoryLimit)
                .ToList();

            foreach (var oldPassword in previousPasswords)
            {
                var passwordVerificationResult = manager.PasswordHasher.VerifyHashedPassword(user, oldPassword.PasswordHash, password);
                if (passwordVerificationResult == PasswordVerificationResult.Success)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "PasswordReuse",
                        Description = "You cannot reuse your previous passwords."
                    });
                }
            }

            return IdentityResult.Success;
        }
    }
}