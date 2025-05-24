using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GorillazDiscordBot.Entity
{
    public class ErgastResponse
    {
        [JsonPropertyName("MRData")]
        public MRData MRData { get; set; }
    }

    public class MRData
    {
        [JsonPropertyName("StandingsTable")]
        public StandingsTable StandingsTable { get; set; }
    }

    public class StandingsTable
    {
        [JsonPropertyName("StandingsLists")]
        public List<StandingsList> StandingsLists { get; set; }
    }

    public class StandingsList
    {
        [JsonPropertyName("DriverStandings")]
        public List<DriverStanding> DriverStandings { get; set; }
    }

    public class DriverStanding
    {
        [JsonPropertyName("position")]
        public string Position { get; set; }

        [JsonPropertyName("points")]
        public string Points { get; set; }

        [JsonPropertyName("wins")]
        public string Wins { get; set; }

        [JsonPropertyName("Driver")]
        public Driver Driver { get; set; }
    }

    public class Driver
    {
        [JsonPropertyName("driverId")]
        public string DriverId { get; set; }

        [JsonPropertyName("givenName")]
        public string GivenName { get; set; }

        [JsonPropertyName("familyName")]
        public string FamilyName { get; set; }

        [JsonPropertyName("nationality")]
        public string Nationality { get; set; }
    }
}
