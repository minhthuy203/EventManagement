using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EventManagement.Models
{
    public class LoginViewModel
    {
        [JsonPropertyName("Email")]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [JsonPropertyName("Password")]
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

    }
}
