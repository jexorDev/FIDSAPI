namespace FIDSAPI.Models
{
    public enum Disposition
    {
        Arrival,
        Departure
    }

    public class BaseAirportFlightModel
    {
        public Disposition Disposition { get; set; }
        public string Status { get; set; } = string.Empty;
        public string AirlineIdentifier { get; set; } = string.Empty;
        public string AirlineName { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public string AirportGate { get; set; } = string.Empty;
        public DateTime? ScheduledDepartureTime { get; set; }
        public DateTime? ActualDepartureTime { get; set; }
        public DateTime? ScheduledArrivalTime { get; set; }
        public DateTime? ActualArrivalTime { get; set; }
        public string CityCode { get; set; } = string.Empty;    
        public string CityName { get; set; } = string.Empty;
        public string CityAirportname { get; set; } = string.Empty;
    }

    
}
