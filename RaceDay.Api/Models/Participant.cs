using System;
using System.Collections.Generic;

namespace RaceDay.Api.Models;

public partial class Participant
{
    public int ParticipantId { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string? EmergencyContact { get; set; }

    public virtual ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();

    public virtual User ParticipantNavigation { get; set; } = null!;
}
