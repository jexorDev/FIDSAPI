using Microsoft.AspNetCore.Mvc;
using FIDSAPI.Models;
using FIDSAPI.Models.FlightAware;
using Newtonsoft.Json;

namespace FIDSAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AirportFlightsController : ControllerBase
    {
        
        private readonly IConfiguration _configuration;
        private readonly ILogger<AirportFlightsController> _logger;

        public AirportFlightsController(ILogger<AirportFlightsController> logger, IConfiguration config)
        {
            _configuration = config;
            _logger = logger;
        }

        [HttpGet(Name = "GetAirportFlights")]
        public async Task<IEnumerable<BaseAirportFlightModel>> Get()
        {
            var flights = new List<BaseAirportFlightModel>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Apikey", _configuration["FlightAwareKey"]);
                var flightAwareResponseObject = await client.GetAsync("https://aeroapi.flightaware.com/aeroapi/airports/KBNA/flights?type=airline");
                var flightAwareResponseBody = flightAwareResponseObject.Content.ReadAsStringAsync().Result;

                if (flightAwareResponseBody == null) return flights;

                FlightAwareAirportFlightsResponseObject flightAwareResponse = JsonConvert.DeserializeObject<FlightAwareAirportFlightsResponseObject>(flightAwareResponseBody);
                
                if (flightAwareResponse != null)
                {
                    foreach (var arrival in flightAwareResponse.arrivals)
                    {
                        flights.Add(new BaseAirportFlightModel
                        {
                            Status = arrival.status,
                            Disposition = Disposition.Arrival,
                            FlightNumber = arrival.flight_number,
                            AirportGate = arrival.gate_destination,
                            AirlineIdentifier = arrival.operator_iata,
                            AirlineName = "", //TODO: Mapping
                            ScheduledDepartureTime = arrival.scheduled_out,
                            ActualDepartureTime = arrival.actual_out,
                            ScheduledArrivalTime = arrival.scheduled_in,
                            ActualArrivalTime = arrival.actual_in,
                            CityCode = arrival.origin.code_iata,
                            CityName = arrival.origin.city,
                            CityAirportname = arrival.origin.name
                        });
                    }
                }

                return flights;
            }
        }
    }
}