using EventManagement.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Data;
using System.Globalization;
using System.Xml.Linq;
namespace EventManagement.Controllers;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "Cookies", Roles = "Organizer")]
public class OrganizersController : Controller
{
    private readonly EventDbContext _context;
    private readonly string OrganizerEmail;
    private readonly ApplicationUser loginOrganizer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EventDbContext> _logger;
    public OrganizersController(EventDbContext context, UserManager<ApplicationUser> userManager, ILogger<EventDbContext> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string evName)
    {
        var organizerEmail = User.Identity.Name.ToString();
        var organizer = _userManager.FindByEmailAsync(organizerEmail).Result;
		//var events = await _context.Events.ToListAsync();
		IEnumerable<Event> ev;
		if (string.IsNullOrEmpty(evName))
		{
			// Không có tìm kiếm, hiển thị toàn bộ dữ liệu
			ev = await _context.Events.Include(e => e.Participants).Where(x => x.OrganizerId == organizer.Id).ToListAsync();
		}
		else
		{
			// Có tìm kiếm, sử dụng phân trang
			ev = await _context.Events.Include(e => e.Participants).Where(x => x.OrganizerId == organizer.Id && x.Title.Contains(evName)).ToListAsync();
			if (ev.Count() == 0)
			{
				ViewBag.NameEventEmpty = "Sự kiện bạn tìm ko tồn tại";
			}
		}
		ViewBag.NameEvent = evName;
		ViewBag.EvCount = ev.Count();
		return View(ev);
    }

    public IActionResult DashBoard()
    {
        var organizerEmail = User.Identity.Name.ToString();
        var organizer = _userManager.FindByEmailAsync(organizerEmail).Result;
        var subcribers = _context.Events.Include(e => e.Participants).Where(x => x.OrganizerId == organizer.Id).ToList();
        ViewBag.subcriberCount = subcribers.Count;
        var events = _context.Events.Where(x => x.OrganizerId == organizer.Id).ToList();
        ViewBag.eventsCount = events.Count;
        return View();
    }

    public IActionResult Subcribers()
    {
        var organizerEmail = User.Identity.Name.ToString();
        var organizer = _userManager.FindByEmailAsync(organizerEmail).Result;
        var events = _context.EventUsers.Include(x => x.Event).Where(x => x.UserId == organizer.Id).ToList();
        
        return View(events);
    }

    public IActionResult CreateEvent()
    {
        return View();
    }
    [HttpPost]
    public IActionResult CreateEvent(Event newEvent)
    {
        var OrganizerEmail = User.Identity.Name.ToString();
        var loginOrganizer = _userManager.FindByEmailAsync(OrganizerEmail).Result;
        try
        {
            if (newEvent == null) return Json(new { success = false, message = "Event is incorrect!!!" });
            // Check if the request contains a file
            newEvent.OrganizerId = loginOrganizer.Id;
            newEvent.Organizer = loginOrganizer;
            
            if (Request.Form.Files.Count > 0)
            {
                var file = Request.Form.Files[0];
                var FileName = GenerateUniqueFileName(file.FileName);
                // Save the uploaded file to a physical location (e.g., wwwroot/images)
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "img_events", FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                    newEvent.Image = FileName; // Update this based on your actual image path
                }

                _context.Events.Add(newEvent);
                _context.SaveChanges();
                return Json(new { success = true, redirectUrl = Url.Action("Index", "Organizers") });
            }
            else
            {
                return Json(new { success = false, message = "No image provided" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message.ToString());
            return Json(new { success = false, message = "Error! An error occurred!!!" });
        }
    }


    [HttpGet]
    public IActionResult EventDetail(int? Id)
    {
        if (Id == null) return NotFound();
        var showEvent = _context.Events.FirstOrDefault(x => x.EventID == Id);
        if (showEvent == null) return NotFound();
        return View(showEvent);
    }

    [HttpGet]
    public async Task<IActionResult> UpdateEvent(int? Id)
    {
        if (Id == null) return NotFound();
        var showEvent = await _context.Events.FirstOrDefaultAsync(x => x.EventID == Id);
       
        if (showEvent == null) return NotFound();

        return View(showEvent);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEvent(Event updateEvent)
    {
        try
        {

            if (updateEvent == null) return NotFound();
            // Retrieve existing event from the database
            var existingEvent = await _context.Events.FirstOrDefaultAsync(e => e.EventID == updateEvent.EventID);

            if (existingEvent == null)
            {
                return Json(new { success = false, message = "Event not found" });
            }
            // Check if the request contains a new file
            if (Request.Form.Files.Count > 1)
            {
                var file = Request.Form.Files[0];
                var FileName = GenerateUniqueFileName(file.FileName);
                // Save the uploaded file to a physical location (e.g., wwwroot/images)
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "img_events", FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                    updateEvent.Image = FileName; // Update this based on your actual image path
                }
            }
            else
            {
                updateEvent.Image = existingEvent.Image;
            }

            _context.Entry(existingEvent).CurrentValues.SetValues(updateEvent);
            await _context.SaveChangesAsync();
            return Json(new { success = true, redirectUrl = Url.Action("EventDetail", "Organizers", new { Id = updateEvent.EventID }) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message.ToString());
            return Json(new { success = false, message = "Error! An error occurred!!!" });
        }
    }
    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int eventId)
    {
        var eventToDelete = await _context.Events.FindAsync(eventId);

        if (eventToDelete == null)
        {
            return Json(new { success = false, message = "\r\nNo event found to delete." });
        }

        _context.Events.Remove(eventToDelete);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "The event has been deleted successfully." });
    }


    private string GenerateUniqueFileName(string fileName)
    {
        var timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var randomString = Guid.NewGuid().ToString("N").Substring(0, 8);

        var uniqueFileName = $"{timeStamp}_{randomString}{Path.GetExtension(fileName)}";

        return uniqueFileName;
    }
}

