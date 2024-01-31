using System.ComponentModel.DataAnnotations;

namespace EventManagement.Models
{
    public class ResetPasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        public string UserId { get; set; }

        public string Token { get; set; }
    }

}
