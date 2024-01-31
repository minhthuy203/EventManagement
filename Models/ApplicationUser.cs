using Microsoft.AspNetCore.Identity;

namespace EventManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public List<EventUser> RegisteredEvents { get; set; }
    }
}
