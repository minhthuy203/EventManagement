using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers
{
	public class BookingConfirmController : Controller
	{
		public IActionResult Index()
		{
			return View();
		}
	}
}
