using System;
using System.Collections.Generic;

namespace RaceDay.Api.Models;

public partial class Event
{
    public int EventId { get; set; }

    public int OrganiserId { get; set; }

    public string EventName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly EventDate { get; set; }

    public string Location { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<EventCategory> EventCategories { get; set; } = new List<EventCategory>();

    public virtual Organiser Organiser { get; set; } = null!;

    public virtual ICollection<Route> Routes { get; set; } = new List<Route>();
}
