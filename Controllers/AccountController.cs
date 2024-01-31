using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EventManagement.Models;
using System.Data;
using EventManagement.Tasks;
using System.Net;
using System.Web;
using System.Text;

namespace EventManagement.Controllers;
public class AccountController : Controller
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly SignInManager<ApplicationUser> _signInManager;
	private readonly ISendMailService _emailSender;

	public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ISendMailService emailSender)
	{
		_userManager = userManager;
		_signInManager = signInManager;
		_emailSender = emailSender;
	}

	[AllowAnonymous]
	public IActionResult Login()
	{
		return View();
	}


	[HttpPost]
	[AllowAnonymous]
	public async Task<IActionResult> Login(LoginViewModel model)
	{
		if (ModelState.IsValid)
		{

			var user = await _userManager.FindByEmailAsync(model.Email);
			if (user != null)
			{
				if (!await _userManager.IsEmailConfirmedAsync(user))
				{
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
					return Json(new { success = false, error = "Please confirm your email before logging in!!!" });
				}
				var role = _userManager.GetRolesAsync(user).Result.FirstOrDefault();
				if (role == null)
				{
					return Json(new { success = false, error = "The account has not been authorized!!!" });
				}
				var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, lockoutOnFailure: false);
				if (result.Succeeded)
				{
					var claims = new List<Claim>
					{
						new Claim(ClaimTypes.Name, user.UserName),
						new Claim(ClaimTypes.Role, role)
					};
					var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

					var authProperties = new AuthenticationProperties
					{
						// Cấu hình các thuộc tính xác thực nếu cần
					};

					await HttpContext.SignInAsync(
						CookieAuthenticationDefaults.AuthenticationScheme,
						new ClaimsPrincipal(claimsIdentity),
						authProperties);

					HttpContext.Session.SetString("Username", user.FirstName);
					HttpContext.Session.SetString("Email", user.Email);
					string route;
					if (role == "User") route = "Home";
					else route = role == "Admin" ? role : "Organizers";
					return Json(new { success = true, redirectUrl = Url.Action("Index", route) });
				}
			}
		}
		return Json(new { success = false, error = "Account or password is incorrect!!" });
	}


	[HttpGet]
	public IActionResult Register()
	{
		return View();
	}

	[HttpPost]
	public async Task<IActionResult> Register(RegisterViewModel model)
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
					await _userManager.AddToRoleAsync(user, "User");
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
						Body = "Visit the following URL to AUTHENTICATE your email" + callbackUrl
					};

					await _emailSender.SendMail(mailData);

					return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
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

	[HttpGet]
	[AllowAnonymous]
	public IActionResult ConfirmEmail(string userId, string safeEncodedToken)
	{
		if (userId == null || safeEncodedToken == null)
		{
			return RedirectToAction("Index", "Home");
		}

		var model = new ConfirmEmailModel { UserId = userId, Token = safeEncodedToken };
		return View(model);
	}


	[HttpPost]
	public async Task<IActionResult> ConfirmEmail(ConfirmEmailModel model)
	{
		if (ModelState.IsValid)
		{
			var user = await _userManager.FindByIdAsync(model.UserId);

			if (user != null)
			{
				var decodedToken = Uri.UnescapeDataString(model.Token);

				var decodedTokenBytes = Convert.FromBase64String(decodedToken);
				var originalToken = Encoding.UTF8.GetString(decodedTokenBytes);
				var result = await _userManager.ConfirmEmailAsync(user, originalToken);

				if (result.Succeeded)
				{
					return Json(new { success = true, redirectUrl = Url.Action("Index", "Organizers") });
				}
				else
				{
					return Json(new { success = false, redirectUrl = Url.Action("Index", "Home") });
				}
			}
		}
		return View(model);
	}


	public async Task<IActionResult> Logout()
	{
		await _signInManager.SignOutAsync();
		HttpContext.Session.Clear();
		await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
		return RedirectToAction("Index", "Home");
	}

	public IActionResult ForgotPassword()
	{
		return View();
	}

	[HttpPost]
	public async Task<IActionResult> ForgotPassword(string email)
	{
		if (ModelState.IsValid)
		{
			var user = await _userManager.FindByEmailAsync(email);
			if (user != null)
			{
				var randomValue = Guid.NewGuid().ToString();

				user.SecurityStamp = randomValue;

				await _userManager.UpdateSecurityStampAsync(user);
				var token = await _userManager.GeneratePasswordResetTokenAsync(user);
				var encodedTokenBytes = Encoding.UTF8.GetBytes(token);
				var encodedToken = Convert.ToBase64String(encodedTokenBytes);

				var safeEncodedToken = Uri.EscapeDataString(encodedToken);
				var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, safeEncodedToken }, protocol: HttpContext.Request.Scheme);

				var mailData = new MailContent
				{
					To = email,
					Subject = "ResetPassword",
					Body = "Visit the following URL to RESET your password " + callbackUrl
				};

				await _emailSender.SendMail(mailData);
				return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });

			}

			// Tránh hiện thông báo là email không tồn tại để ngăn chặn lợi dụng
			return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
		}

		return View(email);
	}

	[HttpGet]
	public async Task<IActionResult> ResetPassword(string userId, string safeEncodedToken)
	{
		var user = await _userManager.FindByIdAsync(userId);
		if (user == null) return NotFound();
		var model = new ResetPasswordViewModel { UserId = userId, Token = safeEncodedToken };
		return View(model);
	}

	[HttpPost]
	public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
	{
		if (ModelState.IsValid)
		{
			var user = await _userManager.FindByIdAsync(model.UserId);
			if (user != null)
			{
				var decodedToken = Uri.UnescapeDataString(model.Token);

				// Decode Base64 để lấy lại token gốc
				var decodedTokenBytes = Convert.FromBase64String(decodedToken);
				var originalToken = Encoding.UTF8.GetString(decodedTokenBytes);
				var result = await _userManager.ResetPasswordAsync(user, originalToken, model.NewPassword);
				if (result.Succeeded)
				{
					// Đặt lại mật khẩu thành công, bạn có thể redirect hoặc hiển thị thông báo thành công
					return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
				}
			}
			else
			{
				return Json(new { success = false, error = "Account doesn't exist!!!" });
			}
		}

		return View(model);
	}


	[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Policy = "Cookies", Roles = "Admin,User,Organizer")]
	public IActionResult ChangePassword()
	{
		return View();
	}

	[HttpPost]
	public IActionResult ChangePassword(ChangePasswordModel model)
	{
		var userEmail = User.Identity.Name;
		var user = _userManager.FindByEmailAsync(userEmail).Result;
		var result = _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword).Result;
		if (result.Succeeded)
		{
			return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
		}
		return Json(new { success = false, message = "Incorrect password!!!" });

	}
}



