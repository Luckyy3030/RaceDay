using System;
using System.Collections.Generic;

namespace RaceDay.Api.Models;

public partial class Enrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }

    public int EventCategoryId { get; set; }

    public DateTime EnrolmentDate { get; set; }

    public string Status { get; set; } = null!;

    public virtual EventCategory EventCategory { get; set; } = null!;

    public virtual Participant Participant { get; set; } = null!;

    public virtual Result? Result { get; set; }
}
