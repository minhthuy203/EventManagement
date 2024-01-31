using EventManagement.Models;
using EventManagement.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EventManagement.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "Cookies", Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EventDbContext _eventContext;
        private readonly ISendMailService _emailSender;
        private readonly ILogger<AdminController> _logger;
        public AdminController(UserManager<ApplicationUser> userManager, ISendMailService emailSender, ILogger<AdminController> logger, EventDbContext eventContext)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
            _eventContext = eventContext;
        }
        public IActionResult Index()
        {
            var organizers = _userManager.GetUsersInRoleAsync("Organizer").Result;
            ViewBag.organizersCount = organizers.Count;
            var events = _eventContext.Events.ToList();
            ViewBag.eventsCount = events.Count;
            var users = _userManager.GetUsersInRoleAsync("User").Result;
            ViewBag.usersCount = users.Count;
            return View();
        }
        public async Task<IActionResult> Events()
        {
            var events = await _eventContext.Events.Include(x => x.Organizer).ToListAsync();
            return View(events);
        } 
        public async Task<IActionResult> EventDetail(int? id)
        {
            if (id == null) return NotFound();
            var showEvent = await _eventContext.Events.Include(x => x.Organizer).FirstOrDefaultAsync(x => x.EventID == id);
            if (showEvent == null) return NotFound();
            return View(showEvent);
        }

        public IActionResult Organizer()
        {
            var organizers = _userManager.GetUsersInRoleAsync("Organizer").Result.ToList();
            return View(organizers);
        }

        public IActionResult CreateOrganizer()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrganizer(RegisterViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FirstName = model.FirstName, LastName = model.LastName };
                    var existedUser = await _userManager.FindByNameAsync(user.Email);
                    if (existedUser != null) return Json(new { success = false, error = "Email already exists!!!" });

                    var result = await _userManager.CreateAsync(user, model.Password);

                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Organizer");
                        var randomValue = Guid.NewGuid().ToString();

                        user.SecurityStamp = randomValue;

                        await _userManager.UpdateSecurityStampAsync(user);
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var encodedTokenBytes = Encoding.UTF8.GetBytes(token);
                        var encodedToken = Convert.ToBase64String(encodedTokenBytes);

                        var safeEncodedToken = Uri.EscapeDataString(encodedToken);
                        var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, safeEncodedToken }, protocol: HttpContext.Request.Scheme);

                        var mailData = new MailContent
                        {
                            To = model.Email,
                            Subject = "Confirm Email",
                            Body = "Visit the following URL to AUTHENTICATE your email " + callbackUrl
                        };

                        await _emailSender.SendMail(mailData);

                        return Json(new { success = true, redirectUrl = Url.Action("Organizer", "Admin") });
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, errors });
            }
            catch (Exception ex)
            {
                return Content(ex.Message.ToString());
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteOrganizer(string Id)
        {
            var user = await _userManager.FindByIdAsync(Id);
            if (user == null) Json(new { success = false, message = "Error! An error occurred!!!!" });
            var eventUsers = _eventContext.EventUsers.Where(x => x.UserId == user.Id).ToList();
            _eventContext.RemoveRange(eventUsers);
            var events = _eventContext.Events.Where(x => x.OrganizerId == user.Id).ToList();
            _eventContext.RemoveRange(events);
            _eventContext.SaveChanges();
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded) return Json(new { success = true, redirectUrl = Url.Action("Organizer", "Admin") });
            return Json(new { success = false, message = "Error! An error occurred!!!!" });
        }

        [HttpGet]
        public async Task<IActionResult> UpdateOrganizer(string Id)
        {
            var user = await _userManager.FindByIdAsync(Id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrganizer(ApplicationUser model)
        {
            if (ModelState.IsValid)
            {
                var user = _userManager.FindByIdAsync(model.Id).Result;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                await _userManager.UpdateAsync(user);
            }
            return Redirect("Organizer");
        }

    }
}
