using System;
using System.Collections.Generic;

namespace RaceDay.Api.Models;

public partial class EventCategory
{
    public int EventCategoryId { get; set; }

    public int EventId { get; set; }

    public int CategoryId { get; set; }

    public decimal EntryFee { get; set; }

    public int MaximumParticipants { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();

    public virtual Event Event { get; set; } = null!;
}
