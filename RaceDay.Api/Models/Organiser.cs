using System;
using System.Collections.Generic;

namespace RaceDay.Api.Models;

public partial class Organiser
{
    public int OrganiserId { get; set; }

    public string OrganisationName { get; set; } = null!;

    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    public virtual User OrganiserNavigation { get; set; } = null!;
}
