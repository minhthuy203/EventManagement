using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers
{
	public class InvoiceController : Controller
	{
		public IActionResult Index()
		{
			return View();
		}
	}
}
