using EventManagement.Models;
using EventManagement.Tasks;
using EventManagement.VNPayClass;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using X.PagedList;

namespace EventManagement.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly EventDbContext _context;
    private readonly IConfiguration _config;
    private readonly ISendMailService _sendMailService;

    public HomeController(ILogger<HomeController> logger, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration config, ISendMailService sendMailService, EventDbContext context)
    {
        _logger = logger;
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _config = config;
        _sendMailService = sendMailService;
    }

    public IActionResult Index(string evName, int page = 1)
    {
        var limit = 8;
        IEnumerable<Event> ev;
        if (string.IsNullOrEmpty(evName))
        {
            // Không có tìm kiếm, hiển thị toàn bộ dữ liệu
            ev = _context.Events.Include(e => e.Participants).OrderBy(x => x.EventID).ToPagedList(page, limit);
        }
        else
        {
            ev = _context.Events.Include(e => e.Participants)
                .Where(e => e.Title.Contains(evName))
                .OrderBy(x => x.EventID).ToPagedList(page, limit);
            if (ev.Count() == 0)
            {
                ViewBag.NameEventEmpty = "The event you are looking for doesn't exist";
            }
        }
        ViewBag.NameEvent = evName;
        return View(ev);
    }

    public IActionResult EventsDetail(int? eventId)
    {

        if (eventId == null) return NotFound();
        var showEvent = _context.Events.Include(e => e.Organizer).FirstOrDefault(x => x.EventID == eventId);
        if (showEvent == null) return NotFound();

        var moreEvents = _context.Events.Where(e => e.EventID != eventId).ToList();
        ViewBag.ShowEventMore = moreEvents;
        bool isBooked = false;
        if (User.Identity.IsAuthenticated)
        {
            var userEmail = User.Identity.Name.ToString();
            var user = _userManager.FindByEmailAsync(userEmail).Result;
            var events = _context.EventUsers.Where(x => x.UserId == user.Id && x.EventId == eventId).ToList();
            if (events.Count > 0)
            {
                isBooked = true;
            }
        }
        var outOfStock = _context.EventUsers.Where(e => e.EventId == showEvent.EventID).ToList();
        ViewBag.OutStock = outOfStock.Count();
        
        ViewBag.isBooked = isBooked;
        return View(showEvent);
    }
    


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }


    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "Cookies", Roles = "User,Organizer")]
    public IActionResult Payment(string eventId, float eventPrice)
    {
        string url = _config.GetSection("VNPay:Url").Value;
        string returnUrl = _config.GetSection("VNPay:ReturnUrl").Value;
        string tmnCode = _config.GetSection("VNPay:TmnCode").Value;
        string hashSecret = _config.GetSection("VNPay:HashSecret").Value;

        PayLib pay = new PayLib();

        pay.AddRequestData("vnp_Version", "2.1.0"); //Phiên bản api mà merchant kết nối. Phiên bản hiện tại là 2.1.0
        pay.AddRequestData("vnp_Command", "pay"); //Mã API sử dụng, mã cho giao dịch thanh toán là 'pay'
        pay.AddRequestData("vnp_TmnCode", tmnCode); //Mã website của merchant trên hệ thống của VNPAY (khi đăng ký tài khoản sẽ có trong mail VNPAY gửi về)
        pay.AddRequestData("vnp_Amount", (eventPrice * 100).ToString()); //số tiền cần thanh toán, công thức: số tiền * 100 - ví dụ 10.000 (mười nghìn đồng) --> 1000000
        pay.AddRequestData("vnp_BankCode", "NCB"); //Mã Ngân hàng thanh toán (tham khảo: https://sandbox.vnpayment.vn/apis/danh-sach-ngan-hang/), có thể để trống, người dùng có thể chọn trên cổng thanh toán VNPAY
        pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss")); //ngày thanh toán theo định dạng yyyyMMddHHmmss
        pay.AddRequestData("vnp_CurrCode", "VND"); //Đơn vị tiền tệ sử dụng thanh toán. Hiện tại chỉ hỗ trợ VND
        pay.AddRequestData("vnp_IpAddr", "127.0.0.1"); //Địa chỉ IP của khách hàng thực hiện giao dịch
        pay.AddRequestData("vnp_Locale", "vn"); //Ngôn ngữ giao diện hiển thị - Tiếng Việt (vn), Tiếng Anh (en)
        pay.AddRequestData("vnp_OrderInfo", eventId); //Thông tin mô tả nội dung thanh toán
        pay.AddRequestData("vnp_OrderType", "other"); //topup: Nạp tiền điện thoại - billpayment: Thanh toán hóa đơn - fashion: Thời trang - other: Thanh toán trực tuyến
        pay.AddRequestData("vnp_ReturnUrl", returnUrl); //URL thông báo kết quả giao dịch khi Khách hàng kết thúc thanh toán
        pay.AddRequestData("vnp_TxnRef", DateTime.Now.Ticks.ToString()); //mã hóa đơn

        string paymentUrl = pay.CreateRequestUrl(url, hashSecret);

        return Redirect(paymentUrl);
    }

    public IActionResult PaymentConfirm()
    {
        if (Request.Query.Count > 0)
        {
            string hashSecret = _config.GetSection("VNPay:HashSecret").Value; //Chuỗi bí mật
            var vnpayData = Request.Query;
            PayLib pay = new PayLib();

            //lấy toàn bộ dữ liệu được trả về
            foreach (var (key, value) in vnpayData)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    pay.AddResponseData(key, value);
                }
            }

            long orderId = Convert.ToInt64(pay.GetResponseData("vnp_TxnRef")); //mã hóa đơn
            long vnpayTranId = Convert.ToInt64(pay.GetResponseData("vnp_TransactionNo")); //mã giao dịch tại hệ thống VNPAY
            string vnp_ResponseCode = pay.GetResponseData("vnp_ResponseCode"); //response code: 00 - thành công, khác 00 - xem thêm https://sandbox.vnpayment.vn/apis/docs/bang-ma-loi/
            string vnp_SecureHash = Request.Query["vnp_SecureHash"]; //hash của dữ liệu trả về
            string eventId = Request.Query["vnp_OrderInfo"];
            bool checkSignature = pay.ValidateSignature(vnp_SecureHash, hashSecret); //check chữ ký đúng hay không?

            if (checkSignature)
            {
                if (vnp_ResponseCode == "00")
                {
                    byte[] qrCodeData = SendMailService.GenerateQrCode(vnpayTranId.ToString());
                    var userEmail = User.Identity.Name.ToString();
                    string recipientEmail = userEmail;
                    var user = _userManager.FindByEmailAsync(recipientEmail).Result;
                    var thisEvent = _context.Events.FirstOrDefault(x => x.EventID == int.Parse(eventId));
                    var newEventUser = new EventUser()
                    {
                        UserId = user.Id,
                        User = user,
                        EventId = thisEvent.EventID,
                        Event = thisEvent
                    };
                    _context.EventUsers.Add(newEventUser);
                    _context.SaveChanges();

                    _sendMailService.SendEmailWithQrCode(qrCodeData, recipientEmail);
                    ViewBag.Message = "Thanh toán thành công hóa đơn " + orderId + " | Mã giao dịch: " + vnpayTranId;
                }
                else
                {
                    //Thanh toán không thành công. Mã lỗi: vnp_ResponseCode
                    ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý hóa đơn " + orderId + " | Mã giao dịch: " + vnpayTranId + " | Mã lỗi: " + vnp_ResponseCode;
                }
            }
            else
            {
                ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý";
            }
            string backUrl = Url.Action("EventsDetail", new { eventId });
            return Redirect(backUrl);
        }
        return NotFound();
    }
}
