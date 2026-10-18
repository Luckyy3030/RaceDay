using System;
using System.Collections.Generic;

namespace RaceDay.Api.Models;

public partial class Route
{
    public int RouteId { get; set; }

    public int EventId { get; set; }

    public string RouteName { get; set; } = null!;

    public decimal DistanceKm { get; set; }

    public string? RouteDescription { get; set; }

    public virtual Event Event { get; set; } = null!;
}
