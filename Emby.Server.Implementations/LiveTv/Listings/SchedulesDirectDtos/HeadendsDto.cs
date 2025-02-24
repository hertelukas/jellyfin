#nullable disable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Server.Implementations.LiveTv.Listings.SchedulesDirectDtos
{
    /// <summary>
    /// Headends dto.
    /// </summary>
    public class HeadendsDto
    {
        /// <summary>
        /// Gets or sets the headend.
        /// </summary>
        [JsonPropertyName("headend")]
        public string Headend { get; set; }

        /// <summary>
        /// Gets or sets the transport.
        /// </summary>
        [JsonPropertyName("transport")]
        public string Transport { get; set; }

        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        [JsonPropertyName("location")]
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the list of lineups.
        /// </summary>
        [JsonPropertyName("lineups")]
        public List<LineupDto> Lineups { get; set; }
    }
}
