using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using GRP_03_27.Models.ViewModels;
using GRP_03_27.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace GRP_03_27.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<CustomerController> _logger;
        private readonly INotificationService _notificationService;

        public CustomerController(ApplicationDbContext context, UserManager<User> userManager, 
            ILogger<CustomerController> logger, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _notificationService = notificationService;
        }

        // ==============================
        // CUSTOMER DASHBOARD
        // ==============================

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["InfoMessage"] = "Please complete your profile to access the dashboard.";
                    return RedirectToAction("CreateProfile");
                }

                // Get customer's fridges
                var fridges = await _context.Fridges
                    .Where(f => f.CustomerId == customer.CustomerId)
                    .Include(f => f.FaultReports)
                    .ToListAsync();

                // Get fault reports
                var faultReports = await _context.FaultReports
                    .Where(fr => fr.CustomerId == customer.CustomerId)
                    .Include(fr => fr.Fridge)
                    .Include(fr => fr.AssignedTechnician)
                    .OrderByDescending(fr => fr.DateReported)
                    .ToListAsync();

                // Count active fridges (all fridges except Scrapped ones)
                var activeFridgesActual = fridges.Count(f => f.Status != FridgeStatus.Scrapped);

                var viewModel = new CustomerDashboardViewModel
                {
                    Customer = customer,
                    Fridges = fridges,
                    TotalFridges = fridges.Count,
                    ActiveFridges = activeFridgesActual,
                    FaultyFridges = fridges.Count(f => f.Status == FridgeStatus.Faulty),
                    TotalFaultReports = faultReports.Count,
                    PendingFaultReports = faultReports.Count(fr => fr.Status == FaultStatus.Reported),
                    InProgressFaultReports = faultReports.Count(fr => fr.Status == FaultStatus.InProgress),
                    ResolvedFaultReports = faultReports.Count(fr => fr.Status == FaultStatus.Resolved),
                    RecentFaultReports = faultReports.Take(5).ToList(),
                    ActiveFaultReports = faultReports.Where(fr => fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer dashboard");
                TempData["ErrorMessage"] = "An error occurred while loading your dashboard.";
                return RedirectToAction("CreateProfile");
            }
        }

        // ==============================
        // CUSTOMER PROFILE CREATION
        // ==============================

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateProfile()
        {
            try
            {
                // Check if customer already exists and is complete
                var existingCustomer = await GetCurrentCustomerAsync();
                if (existingCustomer != null && !string.IsNullOrEmpty(existingCustomer.BusinessName) && existingCustomer.BusinessName != "Please update your business name")
                {
                    TempData["InfoMessage"] = "Profile already exists.";
                    return RedirectToAction("Profile");
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Login", "Account");
                }

                // Pre-populate with user data
                var viewModel = new CustomerProfileViewModel
                {
                    Email = user.Email,
                    ContactPerson = $"{user.FirstName} {user.LastName}",
                    PhoneNumber = user.PhoneNumber
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer profile creation page");
                TempData["ErrorMessage"] = "An error occurred while loading the profile creation page.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateProfile(CustomerProfileViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please fix the validation errors below.";
                    return View(model);
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Login", "Account");
                }

                // Get or create customer
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    // Create new customer if doesn't exist
                    customer = new Customer
                    {
                        UserId = user.Id,
                        IsActive = true,
                        RegistrationDate = DateTime.UtcNow
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync(); // Save to get CustomerId
                }

                // Check for email conflicts in User table
                if (model.Email != user.Email)
                {
                    var existingUserWithEmail = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUserWithEmail != null && existingUserWithEmail.Id != user.Id)
                    {
                        ModelState.AddModelError("Email", "This email address is already in use by another account.");
                        return View(model);
                    }
                }

                // Update customer fields
                customer.BusinessName = model.BusinessName?.Trim();
                customer.ContactPerson = model.ContactPerson?.Trim();
                customer.Email = model.Email?.Trim();
                customer.PhoneNumber = model.PhoneNumber?.Trim();
                customer.PhysicalAddress = model.PhysicalAddress?.Trim();
                customer.Type = model.BusinessType;
                customer.LastUpdated = DateTime.UtcNow;

                // Handle "Other" business type - store separately, don't overwrite BusinessName
                customer.OtherBusinessType = model.BusinessType == CustomerType.Other ? model.OtherBusinessType?.Trim() : null;

                // Update the User record to keep Identity system in sync
                user.Email = model.Email?.Trim();
                user.PhoneNumber = model.PhoneNumber?.Trim();
                user.FirstName = model.ContactPerson?.Split(' ').FirstOrDefault() ?? user.FirstName;
                user.LastName = model.ContactPerson?.Split(' ').Skip(1).FirstOrDefault() ?? user.LastName;

                // Update both records
                var userUpdateResult = await _userManager.UpdateAsync(user);

                if (!userUpdateResult.Succeeded)
                {
                    foreach (var error in userUpdateResult.Errors)
                    {
                        ModelState.AddModelError("", $"User update error: {error.Description}");
                    }
                    return View(model);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer profile created/updated: {CustomerId} for user {UserId}",
                    customer.CustomerId, user.Id);

                TempData["SuccessMessage"] = "Profile completed successfully! Welcome to your dashboard.";
                return RedirectToAction("Dashboard");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating customer profile");
                TempData["ErrorMessage"] = "A database error occurred while creating your profile. Please check your information and try again.";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer profile: {Error}", ex.Message);
                TempData["ErrorMessage"] = $"An error occurred while creating your profile: {ex.Message}. Please try again.";
                return View(model);
            }
        }

        // ==============================
        // CUSTOMER PROFILE
        // ==============================

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == 0)
                {
                    TempData["ErrorMessage"] = "Please log in to view your profile.";
                    return RedirectToAction("Login", "Account");
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive);

                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Profile not found.";
                    return RedirectToAction("CreateProfile");
                }

                var viewModel = new CustomerProfileViewModel
                {
                    CustomerId = customer.CustomerId,
                    BusinessName = customer.BusinessName,
                    ContactPerson = customer.ContactPerson,
                    Email = customer.Email,
                    PhoneNumber = customer.PhoneNumber ?? "",
                    PhysicalAddress = customer.PhysicalAddress ?? "",
                    BusinessType = customer.Type,
                    OtherBusinessType = customer.OtherBusinessType ?? ""
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer profile");
                TempData["ErrorMessage"] = "An error occurred while loading your profile.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(CustomerProfileViewModel model)
        {
            try
            {
                // Get current customer
                var currentCustomer = await GetCurrentCustomerAsync();
                if (currentCustomer == null)
                {
                    TempData["ErrorMessage"] = "Please log in to edit your profile.";
                    return RedirectToAction("Login", "Account");
                }

                // Ensure the customer can only edit their own profile
                if (model.CustomerId != currentCustomer.CustomerId)
                {
                    TempData["ErrorMessage"] = "Unauthorized access. You can only edit your own profile.";
                    return RedirectToAction("Profile");
                }

                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please fix the validation errors below.";
                    return View(model);
                }

                // Get the associated user record
                var user = await _userManager.FindByIdAsync(currentCustomer.UserId);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "User account not found. Please contact support.";
                    return RedirectToAction("Profile");
                }

                // Check for email conflicts in User table
                if (model.Email != user.Email)
                {
                    var existingUserWithEmail = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUserWithEmail != null && existingUserWithEmail.Id != user.Id)
                    {
                        ModelState.AddModelError("Email", "This email address is already in use by another account.");
                        return View(model);
                    }
                }

                // Update the customer fields
                currentCustomer.BusinessName = model.BusinessName?.Trim();
                currentCustomer.ContactPerson = model.ContactPerson?.Trim();
                currentCustomer.PhoneNumber = model.PhoneNumber?.Trim();
                currentCustomer.PhysicalAddress = model.PhysicalAddress?.Trim();
                currentCustomer.Email = model.Email?.Trim();
                currentCustomer.LastUpdated = DateTime.UtcNow;

                // Update business type
                currentCustomer.Type = model.BusinessType;
                currentCustomer.OtherBusinessType = model.BusinessType == CustomerType.Other ? model.OtherBusinessType?.Trim() : null;

                // Update the User record to keep Identity system in sync
                user.Email = model.Email?.Trim();
                user.PhoneNumber = model.PhoneNumber?.Trim();
                user.FirstName = model.ContactPerson?.Split(' ').FirstOrDefault() ?? user.FirstName;
                user.LastName = model.ContactPerson?.Split(' ').Skip(1).FirstOrDefault() ?? user.LastName;

                // Update both records
                _context.Customers.Update(currentCustomer);
                var userUpdateResult = await _userManager.UpdateAsync(user);

                if (!userUpdateResult.Succeeded)
                {
                    foreach (var error in userUpdateResult.Errors)
                    {
                        ModelState.AddModelError("", $"User update error: {error.Description}");
                    }
                    return View(model);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer profile updated: {CustomerId}", currentCustomer.CustomerId);
                TempData["SuccessMessage"] = "Profile updated successfully!";
                return RedirectToAction("Dashboard");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating customer profile");
                TempData["ErrorMessage"] = "An error occurred while updating your profile. Please try again.";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer profile");
                TempData["ErrorMessage"] = "An error occurred while updating your profile. Please try again.";
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerAddress()
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == 0)
                {
                    return Json(new { success = false, message = "Not authenticated" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                // Check if address is populated
                var hasAddress = !string.IsNullOrWhiteSpace(customer.PhysicalAddress);

                if (!hasAddress)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Address not complete",
                        hasAddress = hasAddress
                    });
                }

                return Json(new
                {
                    success = true,
                    address = customer.PhysicalAddress.Trim(),
                    message = "Address loaded successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCustomerAddress");
                return Json(new { success = false, message = "Error loading address" });
            }
        }

        // POST: Customer/UpdateLocationFromAddress
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLocationFromAddress(string address)
        {
            try
            {
                var customerId = await GetCurrentCustomerIdAsync();
                if (customerId == 0)
                {
                    return Json(new { success = false, message = "Not authenticated" });
                }

                if (string.IsNullOrWhiteSpace(address))
                {
                    return Json(new { success = false, message = "Address is required" });
                }

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.IsActive);

                if (customer == null)
                {
                    return Json(new { success = false, message = "Customer not found" });
                }

                // Update customer address
                customer.PhysicalAddress = address.Trim();
                customer.LastUpdated = DateTime.UtcNow;

                // Try to geocode the address (simplified version - in production you'd use a real geocoding service)
                var geocodedLocation = await GeocodeAddressAsync(address.Trim());
                
                if (geocodedLocation != null)
                {
                    // Check if location exists, if not create it
                    var location = await _context.Locations
                        .FirstOrDefaultAsync(l => l.Address == address.Trim());

                    if (location == null)
                    {
                        location = new Location
                        {
                            Address = address.Trim(),
                            Latitude = geocodedLocation.Latitude,
                            Longitude = geocodedLocation.Longitude,
                            CreatedDate = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.Locations.Add(location);
                        await _context.SaveChangesAsync();
                    }

                    // Update customer with location reference if needed
                    // Note: You might need to add a LocationId field to Customer model
                }

                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer address updated: {CustomerId} - {Address}", customerId, address);

                return Json(new
                {
                    success = true,
                    message = "Address updated successfully",
                    address = address.Trim(),
                    location = geocodedLocation
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer address");
                return Json(new { success = false, message = "Error updating address" });
            }
        }

        // Helper method to geocode address (simplified - replace with real geocoding service)
        private async Task<dynamic> GeocodeAddressAsync(string address)
        {
            try
            {
                // This is a simplified geocoding implementation
                // In production, you would use a real geocoding service like Google Maps API, Bing Maps API, etc.
                
                // For now, return mock coordinates for South African addresses
                // You can replace this with actual API calls
                
                await Task.Delay(100); // Simulate API call delay
                
                // Mock geocoding - in reality you'd call a geocoding service
                return new
                {
                    Latitude = -26.2041 + (new Random().NextDouble() - 0.5) * 0.1, // Johannesburg area
                    Longitude = 28.0473 + (new Random().NextDouble() - 0.5) * 0.1,
                    FormattedAddress = address,
                    Confidence = 0.8
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error geocoding address: {Address}", address);
                return null;
            }
        }

        // ==============================
        // MISSING ACTIONS (Added to fix 404/405 errors)
        // ==============================

        // GET: Customer/EditProfile - Alias for Profile action
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            return await Profile();
        }

        // GET: Customer/ReportFault
        [HttpGet]
        public async Task<IActionResult> ReportFault()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Please complete your business profile to report faults.";
                    return RedirectToAction("CreateProfile");
                }

                var viewModel = new CreateFaultReportViewModel
                {
                    AvailableFridges = await _context.Fridges
                        .Where(f => f.CustomerId == customer.CustomerId && f.Status != FridgeStatus.Scrapped)
                        .OrderBy(f => f.FridgeId)
                        .ToListAsync()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fault report page");
                TempData["ErrorMessage"] = "An error occurred while loading the fault report page.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Customer/MyFridges
        [HttpGet]
        public async Task<IActionResult> MyFridges()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Please complete your business profile to view your fridges.";
                    return RedirectToAction("CreateProfile");
                }

                var fridges = await _context.Fridges
                    .Where(f => f.CustomerId == customer.CustomerId && f.Status != FridgeStatus.Scrapped)
                    .Include(f => f.Supplier)
                    .Include(f => f.FaultReports)
                    .OrderBy(f => f.Status)
                    .ThenBy(f => f.FridgeId)
                    .ToListAsync();

                return View(fridges);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer fridges");
                TempData["ErrorMessage"] = "An error occurred while loading your fridges.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Customer/MyFaultReports
        [HttpGet]
        public async Task<IActionResult> MyFaultReports()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Please complete your business profile to view fault reports.";
                    return RedirectToAction("CreateProfile");
                }

                var faultReports = await _context.FaultReports
                    .Where(fr => fr.CustomerId == customer.CustomerId)
                    .Include(fr => fr.Fridge)
                    .Include(fr => fr.AssignedTechnician)
                    .OrderByDescending(fr => fr.DateReported)
                    .ToListAsync();

                return View(faultReports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer fault reports");
                TempData["ErrorMessage"] = "An error occurred while loading your fault reports.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Customer/RequestNewFridge
        [HttpGet]
        public async Task<IActionResult> RequestNewFridge()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Please complete your business profile to request new fridges.";
                    return RedirectToAction("CreateProfile");
                }

                var model = new NewFridgeRequest
                {
                    Quantity = 1 // Set default value
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading new fridge request page");
                TempData["ErrorMessage"] = "An error occurred while loading the new fridge request page.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Customer/DeactivateAccount
        [HttpGet]
        public async Task<IActionResult> DeactivateAccount()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Please complete your business profile first.";
                    return RedirectToAction("CreateProfile");
                }

                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading account deactivation page");
                TempData["ErrorMessage"] = "An error occurred while loading the account deactivation page.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Customer/MaintenanceHistory
        [HttpGet]
        public async Task<IActionResult> MaintenanceHistory(string statusFilter = "All", string typeFilter = "All")
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Please complete your business profile to view maintenance history.";
                    return RedirectToAction("CreateProfile");
                }

                var maintenanceRecords = await _context.MaintenanceRecords
                    .Where(m => m.Fridge.CustomerId == customer.CustomerId)
                    .Include(m => m.Fridge)
                    .Include(m => m.AssignedTechnician)
                    .OrderByDescending(m => m.ScheduledDate)
                    .ToListAsync();

                // Apply filters
                if (statusFilter != "All" && Enum.TryParse<MaintenanceStatus>(statusFilter, out var status))
                {
                    maintenanceRecords = maintenanceRecords.Where(m => m.Status == status).ToList();
                }

                if (typeFilter != "All" && Enum.TryParse<MaintenanceType>(typeFilter, out var type))
                {
                    maintenanceRecords = maintenanceRecords.Where(m => m.MaintenanceType == type).ToList();
                }

                ViewBag.StatusFilter = statusFilter;
                ViewBag.TypeFilter = typeFilter;

                return View(maintenanceRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer maintenance history");
                TempData["ErrorMessage"] = "An error occurred while loading maintenance history.";
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Customer/UpcomingMaintenance
        [HttpGet]
        public async Task<IActionResult> UpcomingMaintenance()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Please complete your business profile to view upcoming maintenance.";
                    return RedirectToAction("CreateProfile");
                }

                var upcomingMaintenance = await _context.MaintenanceRecords
                    .Where(m => m.Fridge.CustomerId == customer.CustomerId && 
                               m.Status == MaintenanceStatus.Scheduled &&
                               m.ScheduledDate >= DateTime.UtcNow.AddDays(-7)) // Include overdue maintenance
                    .Include(m => m.Fridge)
                    .Include(m => m.AssignedTechnician)
                    .OrderBy(m => m.ScheduledDate)
                    .ToListAsync();

                return View(upcomingMaintenance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer upcoming maintenance");
                TempData["ErrorMessage"] = "An error occurred while loading upcoming maintenance.";
                return RedirectToAction("Dashboard");
            }
        }

        // ==============================
        // EXISTING METHODS (Updated with navigation badges)
        // ==============================


        // POST: Customer/ReportFault - Enhanced Version
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportFault(CreateFaultReportViewModel viewModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed for fault report");
                    await ReloadFaultReportViewModel(viewModel);
                    return View(viewModel);
                }

                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer profile not found.";
                    return RedirectToAction("CreateProfile");
                }

                // Enhanced validation: Verify fridge belongs to customer and is active
                var fridge = await _context.Fridges
                    .FirstOrDefaultAsync(f =>
                        f.FridgeId == viewModel.FridgeId &&
                        f.CustomerId == customer.CustomerId &&
                        f.Status != FridgeStatus.Scrapped);

                if (fridge == null)
                {
                    _logger.LogWarning("Invalid fridge selection attempt: Fridge {FridgeId} by Customer {CustomerId}",
                        viewModel.FridgeId, customer.CustomerId);
                    TempData["ErrorMessage"] = "Invalid fridge selection or fridge is no longer active.";
                    return RedirectToAction("ReportFault");
                }

                // Enhanced validation: Check for duplicate active fault reports
                var existingActiveReport = await _context.FaultReports
                    .AnyAsync(fr =>
                        fr.FridgeId == viewModel.FridgeId &&
                        fr.CustomerId == customer.CustomerId &&
                        (fr.Status == FaultStatus.Reported ||
                         fr.Status == FaultStatus.Diagnosed ||
                         fr.Status == FaultStatus.Scheduled));

                if (existingActiveReport)
                {
                    ModelState.AddModelError("", "This fridge already has an active fault report. Please wait for the current report to be resolved.");
                    await ReloadFaultReportViewModel(viewModel);
                    return View(viewModel);
                }

                // Enhanced validation: Description quality check
                if (viewModel.Description.Trim().Length < 10)
                {
                    ModelState.AddModelError("Description", "Please provide a more detailed description of the fault (minimum 10 characters).");
                    await ReloadFaultReportViewModel(viewModel);
                    return View(viewModel);
                }

                var faultReport = new FaultReport
                {
                    FridgeId = viewModel.FridgeId,
                    CustomerId = customer.CustomerId,
                    Description = SanitizeInput(viewModel.Description.Trim()),
                    RequestReplacement = viewModel.RequestReplacement,
                    CustomerNotes = SanitizeInput(viewModel.CustomerNotes?.Trim()),
                    DateReported = DateTime.UtcNow,
                    Status = FaultStatus.Reported
                };

                // Update fridge status to faulty with validation
                if (fridge.Status != FridgeStatus.Faulty)
                {
                    fridge.Status = FridgeStatus.Faulty;
                    fridge.LastUpdated = DateTime.UtcNow;
                    _context.Update(fridge);
                }

                _context.FaultReports.Add(faultReport);
                await _context.SaveChangesAsync();

                // Create automatic notification
                await _notificationService.CreateFaultReportNotificationAsync(faultReport);

                // Audit trail
                _logger.LogInformation("Fault report created successfully: ID {FaultReportId} for fridge {FridgeId} by customer {CustomerId}",
                    faultReport.FaultReportId, viewModel.FridgeId, customer.CustomerId);

                TempData["SuccessMessage"] = "Fault reported successfully! A technician will contact you within 24 hours.";
                return RedirectToAction("MyFaultReports");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating fault report");
                TempData["ErrorMessage"] = "A database error occurred while reporting the fault. Please try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating fault report");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
            }

            await ReloadFaultReportViewModel(viewModel);
            return View(viewModel);
        }

        // POST: Customer/RequestNewFridge - Enhanced Version
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestNewFridge(NewFridgeRequest newFridgeRequest)
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    return RedirectToAction("CreateProfile");
                }

                if (!ModelState.IsValid)
                {
                    // Log validation errors for debugging
                    var validationErrors = new List<string>();
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning("Validation error in RequestNewFridge: {Error}", error.ErrorMessage);
                        validationErrors.Add(error.ErrorMessage);
                    }
                    
                    TempData["ErrorMessage"] = $"Please correct the errors below: {string.Join(", ", validationErrors)}";
                    return View(newFridgeRequest);
                }

                // Check for recent duplicate requests (within 30 days)
                var recentDuplicate = await _context.NewFridgeRequests
                    .AnyAsync(nfr =>
                        nfr.CustomerId == customer.CustomerId &&
                        nfr.Status == NewFridgeRequestStatus.Pending &&
                        nfr.RequestDate >= DateTime.UtcNow.AddDays(-30));

                if (recentDuplicate)
                {
                    TempData["WarningMessage"] = "You already have a pending new fridge request. Please wait for our Customer Liaison team to review your previous request.";
                    return RedirectToAction("MyFridgeRequests");
                }

                // Create the new fridge request using form data
                var request = new NewFridgeRequest
                {
                    CustomerId = customer.CustomerId, // Set programmatically from authenticated customer
                    RequestedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Quantity = newFridgeRequest.Quantity,
                    BusinessJustification = SanitizeInput(newFridgeRequest.BusinessJustification.Trim()),
                    AdditionalNotes = !string.IsNullOrEmpty(newFridgeRequest.AdditionalNotes) ? SanitizeInput(newFridgeRequest.AdditionalNotes.Trim()) : null,
                    RequestDate = DateTime.UtcNow,
                    Status = NewFridgeRequestStatus.Pending,
                    Priority = PriorityLevel.Medium
                };

                _context.NewFridgeRequests.Add(request);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New fridge request created successfully by customer {CustomerId} for {Quantity} fridges",
                    customer.CustomerId, newFridgeRequest.Quantity);

                TempData["SuccessMessage"] = $"Your fridge request has been submitted successfully! Request ID: #{request.NewFridgeRequestId}. Our Customer Liaison team will review your request and contact you within 2-3 business days.";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new fridge request for customer {CustomerId}", (await GetCurrentCustomerAsync())?.CustomerId);
                TempData["ErrorMessage"] = "An error occurred while submitting your request. Please try again.";
                return View(newFridgeRequest);
            }
        }

        // GET: Customer/MyFridgeRequests
        [HttpGet]
        public async Task<IActionResult> MyFridgeRequests()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer profile not found.";
                    return RedirectToAction("CreateProfile");
                }

                var requests = await _context.NewFridgeRequests
                    .Where(nfr => nfr.CustomerId == customer.CustomerId)
                    .OrderByDescending(nfr => nfr.RequestDate)
                    .ToListAsync();

                return View(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fridge requests for customer");
                TempData["ErrorMessage"] = "An error occurred while loading your fridge requests.";
                return RedirectToAction(nameof(Dashboard));
            }
        }

        // GET: Customer/FaultReportDetails/5 - Enhanced Version
        public async Task<IActionResult> FaultReportDetails(int? id)
        {
            if (id == null || id <= 0)
            {
                _logger.LogWarning("Invalid fault report ID requested: {FaultReportId}", id);
                return NotFound();
            }

            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    return RedirectToAction("CreateProfile");
                }

                var faultReport = await _context.FaultReports
                    .Include(fr => fr.Customer)
                    .Include(fr => fr.Fridge)
                    .Include(fr => fr.AssignedTechnician)
                    .FirstOrDefaultAsync(fr => fr.FaultReportId == id && fr.CustomerId == customer.CustomerId);

                if (faultReport == null)
                {
                    _logger.LogWarning("Fault report not found or access denied: ID {FaultReportId} for customer {CustomerId}",
                        id, customer.CustomerId);
                    return NotFound();
                }

                return View(faultReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fault report details for ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading fault report details.";
                return RedirectToAction("MyFaultReports");
            }
        }

        // POST: Customer/DeactivateAccount - Enhanced Version
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateAccountConfirmed()
        {
            try
            {
                var customer = await GetCurrentCustomerAsync();
                if (customer == null)
                {
                    return RedirectToAction("CreateProfile");
                }

                // Enhanced validation: Check for active fault reports
                var activeFaultReports = await _context.FaultReports
                    .AnyAsync(fr =>
                        fr.CustomerId == customer.CustomerId &&
                        fr.Status != FaultStatus.Resolved &&
                        fr.Status != FaultStatus.Closed);

                if (activeFaultReports)
                {
                    TempData["ErrorMessage"] = "Cannot deactivate account while you have active fault reports. Please resolve all pending reports first.";
                    return RedirectToAction("DeactivateAccount");
                }

                // Check for allocated fridges
                var allocatedFridges = await _context.Fridges
                    .AnyAsync(f =>
                        f.CustomerId == customer.CustomerId &&
                        f.Status == FridgeStatus.Allocated);

                if (allocatedFridges)
                {
                    TempData["ErrorMessage"] = "Cannot deactivate account while you have allocated fridges. Please contact customer support to arrange return.";
                    return RedirectToAction("DeactivateAccount");
                }

                customer.IsActive = false;
                customer.LastUpdated = DateTime.UtcNow;

                _context.Update(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Customer account deactivated successfully: {BusinessName} (ID: {CustomerId})",
                    customer.BusinessName, customer.CustomerId);

                // Sign out the user
                await HttpContext.SignOutAsync();

                TempData["SuccessMessage"] = "Your account has been deactivated successfully. You can reactivate it by contacting customer support.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating customer account");
                TempData["ErrorMessage"] = "An error occurred while deactivating your account. Please try again or contact support.";
                return RedirectToAction("DeactivateAccount");
            }
        }

        #region Helper Methods

        private async Task<int> GetCurrentCustomerIdAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            return customer?.CustomerId ?? 0;
        }

        private async Task<Customer> GetCurrentCustomerAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return null;

                return await _context.Customers
                    .Include(c => c.Fridges)
                    .Include(c => c.FaultReports)
                    .FirstOrDefaultAsync(c => c.UserId == user.Id && c.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current customer");
                return null;
            }
        }

        private async Task ReloadFaultReportViewModel(CreateFaultReportViewModel viewModel)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer != null)
            {
                viewModel.AvailableFridges = await _context.Fridges
                    .Where(f => f.CustomerId == customer.CustomerId && f.Status != FridgeStatus.Scrapped)
                    .OrderBy(f => f.FridgeId)
                    .ToListAsync();
            }
        }

        private async Task SetNavigationBadgeCounts()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer != null)
            {
                // Count active faults (fridges with active fault reports)
                var activeFaultsCount = await _context.Fridges
                    .CountAsync(f => f.CustomerId == customer.CustomerId &&
                                   f.FaultReports.Any(fr =>
                                       fr.Status != FaultStatus.Resolved &&
                                       fr.Status != FaultStatus.Closed));

                // Count pending fault reports
                var pendingReportsCount = await _context.FaultReports
                    .CountAsync(fr => fr.CustomerId == customer.CustomerId &&
                                    (fr.Status == FaultStatus.Reported ||
                                     fr.Status == FaultStatus.Diagnosed));

                ViewBag.ActiveFaultsCount = activeFaultsCount;
                ViewBag.PendingReportsCount = pendingReportsCount;
            }
            else
            {
                ViewBag.ActiveFaultsCount = 0;
                ViewBag.PendingReportsCount = 0;
            }
        }

        private bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return false;
            var saPhoneRegex = new Regex(@"^(\+27|0)[1-8][0-9]{8}$");
            return saPhoneRegex.IsMatch(phoneNumber.Trim());
        }

        private bool IsValidPersonName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var nameRegex = new Regex(@"^[a-zA-Z\s\-']{2,100}$");
            return nameRegex.IsMatch(name.Trim());
        }

        private string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return Regex.Replace(input, @"<[^>]*>", string.Empty);
        }

        #endregion
    }
}