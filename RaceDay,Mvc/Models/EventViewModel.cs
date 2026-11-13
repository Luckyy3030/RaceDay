
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RaceDay.Mvc.Models;

public class EventViewModel
{
    public int EventId { get; set; }

    [JsonPropertyName("EventName")]
    [Required(ErrorMessage = "Event name is required.")]
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Event date is required.")]
    public DateOnly EventDate { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    public string Location { get; set; } = "";

    [JsonPropertyName("DistanceKm")]
    [Range(0.1, 1000, ErrorMessage = "Distance must be greater than zero.")]
    public decimal Distance { get; set; }

    [Required(ErrorMessage = "Event type is required.")]
    public string EventType { get; set; } = "";
}
