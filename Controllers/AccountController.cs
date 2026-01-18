using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GRP_03_27.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult AccessDenied(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ==============================
        // AJAX VALIDATION
        // ==============================
        [HttpGet]
        public async Task<IActionResult> CheckEmail(string email)
            => Json(new { exists = await _userManager.Users.AnyAsync(u => u.NormalizedEmail == email.ToUpper()) });

        [HttpGet]
        public async Task<IActionResult> CheckUsername(string username)
            => Json(new { exists = await _userManager.Users.AnyAsync(u => u.NormalizedUserName == username.ToUpper()) });

        [HttpGet]
        public async Task<IActionResult> CheckFullName(string firstName, string lastName)
        {
            var fullName = $"{firstName} {lastName}".Trim();

            var allUsers = _userManager.Users.ToList();
            foreach (var existingUser in allUsers)
            {
                var claims = await _userManager.GetClaimsAsync(existingUser);
                if (claims.Any(c => c.Type == "FullName" && c.Value == fullName))
                {
                    return Json(new { exists = true });
                }
            }

            return Json(new { exists = false });
        }

        [HttpGet]
        public async Task<IActionResult> CheckPhoneNumber(string phoneNumber)
            => Json(new { exists = await _userManager.Users.AnyAsync(u => u.PhoneNumber == phoneNumber) });

        // ==============================
        // REGISTER
        // ==============================
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterUser model)
        {
            if (!ModelState.IsValid) return View(model);

            // Check if email already exists
            if (await _userManager.Users.AnyAsync(u => u.NormalizedEmail == model.Email.ToUpper()))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(model);
            }

            // Check if username already exists
            if (await _userManager.Users.AnyAsync(u => u.NormalizedUserName == model.Username.ToUpper()))
            {
                ModelState.AddModelError("Username", "This username is already taken.");
                return View(model);
            }

            // Check if phone number already exists
            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "This phone number is already registered.");
                return View(model);
            }

            // Check if full name already exists
            var fullName = $"{model.FirstName} {model.LastName}".Trim();
            var allUsers = _userManager.Users.ToList();
            foreach (var existingUser in allUsers)
            {
                var claims = await _userManager.GetClaimsAsync(existingUser);
                if (claims.Any(c => c.Type == "FullName" && c.Value == fullName))
                {
                    ModelState.AddModelError("LastName", "A customer with this full name already exists.");
                    return View(model);
                }
            }

            // Create user
            var user = new User
            {
                UserName = model.Username,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Add customer role
                await _userManager.AddToRoleAsync(user, "Customer");

                // === CREATE CUSTOMER RECORD ===
                try
                {
                    // Create customer record linked to the user
                    var customer = new Customer
                    {
                        UserId = user.Id, // This links the customer to the user
                        BusinessName = $"{model.FirstName} {model.LastName}'s Business",
                        ContactPerson = $"{model.FirstName} {model.LastName}",
                        PhoneNumber = model.PhoneNumber,
                        Email = model.Email,
                        PhysicalAddress = "Please update your address",
                        Type = CustomerType.SpazaShop,
                        RegistrationDate = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("SUCCESS: Created customer record ID {CustomerId} for user {Email}", customer.CustomerId, user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ERROR creating customer record for user {Email}", user.Email);
                    // Don't throw - allow user registration to succeed even if customer creation fails
                    TempData["Warning"] = "Account created but customer profile setup may need completion.";
                }
                // === END CUSTOMER RECORD CREATION ===

                // Add FullName claim
                await _userManager.AddClaimAsync(user, new Claim("FullName", fullName));

                // Save password history
                var passwordHash = _userManager.PasswordHasher.HashPassword(user, model.Password);
                _context.PasswordHistories.Add(new PasswordHistory
                {
                    UserId = user.Id,
                    PasswordHash = passwordHash,
                    CreatedDate = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                // FIXED: Redirect to CreateProfile instead of EditProfile
                TempData["Success"] = "Account created successfully! Please complete your business profile.";
                return RedirectToAction("CreateProfile", "Customer");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ==============================
        // LOGIN
        // ==============================
        [HttpGet]
        public IActionResult Login(bool passwordResetSuccess = false)
        {
            if (passwordResetSuccess)
                TempData["Success"] = "Password reset successful. Please log in with your new password.";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(Login model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check if username exists
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                ModelState.AddModelError("", "Username not found. Please register first.");
                return View(model);
            }

            // Check if password is correct
            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                ModelState.AddModelError("", "Incorrect password. Did you forget your password?");
                return View(model);
            }

            // Sign in the user
            await _signInManager.SignInAsync(user, model.RememberMe);

            // Unified redirect to role-aware dashboard
            return RedirectToAction("Dashboard", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ==============================
        // PASSWORD RESET
        // ==============================
        [HttpGet]
        public IActionResult VerifyEmail() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(ForgotPassword model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["Error"] = "Customer not registered with this email address.";
                return View(model);
            }

            return View("ResetPassword", new ResetPassword { Email = user.Email });
        }

        [HttpGet]
        public IActionResult ResetPassword(string email = null)
            => View(new ResetPassword { Email = email });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPassword model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Customer not found.");
                return View(model);
            }

            // Check if the new password has been used before
            var oldHashes = _context.PasswordHistories
                .Where(p => p.UserId == user.Id)
                .Select(p => p.PasswordHash)
                .ToList();

            var hasher = new PasswordHasher<User>();
            foreach (var hash in oldHashes)
            {
                if (hasher.VerifyHashedPassword(user, hash, model.Password) == PasswordVerificationResult.Success)
                {
                    ModelState.AddModelError("", "You cannot reuse a previous password. Please choose a new one.");
                    return View(model);
                }
            }

            // Generate token and reset password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            // Save new password to history
            var newPasswordHash = _userManager.PasswordHasher.HashPassword(user, model.Password);
            _context.PasswordHistories.Add(new PasswordHistory
            {
                UserId = user.Id,
                PasswordHash = newPasswordHash,
                CreatedDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password reset successful. Please log in with your new password.";
            return RedirectToAction("Login", new { passwordResetSuccess = true });
        }
    }
}