namespace EventManagement.Models;
public class Event
{
    public int EventID { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; }
    public string Image {  get; set; }
    public int QuantityTickets { get; set; }
    public float Price { get; set; }
    public string OrganizerId { get; set; }
    public ApplicationUser Organizer { get; set; }

    public List<EventUser> Participants { get; set; }
}
