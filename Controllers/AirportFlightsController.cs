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

        private Dictionary<string, List<string>> Airlines = new Dictionary<string, List<string>>()
        {
            //KEY: ICAO, VALUE: USER FRIENDLY, IATA
            {"AAY", new List<string> { "ALLEGIANT", "G4" } },
            {"AAL", new List<string> { "AMERICAN", "AA" } },
            {"ASA", new List<string> { "ALASKA", "AS" } },
            {"BAW", new List<string> { "BRITISH", "BA" } },
            {"VTE", new List<string> { "CONTOUR", "LF" } },
            {"DAL", new List<string> { "DELTA", "DL" } },
            {"EDV", new List<string> { "ENDEAVOR", "9E" } },
            {"MQ", new List<string> { "ENVOY", "MQ" } }, //AMERICAN AIRLINES
            {"FFT", new List<string> { "FRONTIER", "F9" } },
            {"JBU", new List<string> { "JETBLUE", "B6" } },
            {"JIA", new List<string> { "PSA", "OH" } }, //AMERICAN AIRLINES
            {"MEP", new List<string> { "REPUBLIC", "YX" } }, //TODO: CHECK THIS
            {"NKS", new List<string> { "SPIRIT", "NK" } },
            {"SKW", new List<string> { "SKYWEST", "OO" } },
            {"SWA", new List<string> { "SOUTHWEST", "WN" } },
            {"UAL", new List<string> { "UNITED", "UA" } }
        };

        public enum DispositionFilter
        {
            None,
            Arrived,
            Departed,
            ScheduledArriving,
            ScheduledDepartures
        }

        public enum TimeFilter
        {
            Between,
            At
        }

        public AirportFlightsController(ILogger<AirportFlightsController> logger, IConfiguration config)
        {
            _configuration = config;
            _logger = logger;
        }

        [HttpGet(Name = "GetAirportFlights")]
        public async Task<IEnumerable<BaseAirportFlightModel>> Get(string parmString)
        {
            var parmsList = parmString.Split("-");

            DispositionFilter disposition = DispositionFilter.None;
            TimeFilter timeType = TimeFilter.Between;
            var timeFrom = DateTime.Now;
            var timeTo = GetToTime();
            var timeAt = DateTime.Now;
            var airline = string.Empty;
            var airport = string.Empty;
            var apiEndpoint = string.Empty;

            try
            {
                foreach (var parm in parmsList)
                {
                    if (parm.StartsWith("arriving"))
                    {
                        disposition = DispositionFilter.ScheduledArriving;
                        var parms = parm.Split(" ");
                        airline = GetAirline(parms[1]);
                        apiEndpoint = "scheduled_arrivals";
                    }
                    else if (parm.StartsWith("departing"))
                    {
                        disposition = DispositionFilter.ScheduledDepartures;
                        var parms = parm.Split(" ");
                        airline = GetAirline(parms[1]);
                        apiEndpoint = "scheduled_departures";
                    }
                    else if (parm.StartsWith("arrived"))
                    {
                        disposition = DispositionFilter.Arrived;
                        var parms = parm.Split(" ");
                        airline = GetAirline(parms[1]);
                        apiEndpoint = "arrivals";
                    }
                    else if (parm.StartsWith("departed"))
                    {
                        disposition = DispositionFilter.Departed;
                        var parms = parm.Split(" ");
                        airline = GetAirline(parms[1]);
                        apiEndpoint = "departures";
                    }

                    if (parm.StartsWith("between"))
                    {
                        timeType = TimeFilter.Between;
                        var parms = parm.Split(" ");
                        timeFrom = ParseDateFromString(parms[1]);
                        timeTo = ParseDateFromString(parms[2]);

                    }
                    else if (parm.StartsWith("at"))
                    {
                        timeType = TimeFilter.At;
                        var parms = parm.Split(" ");
                        timeAt = ParseDateFromString(parms[1]);
                    }
                }

            }
            catch (Exception ex)
            {
                //return BadRequest("Unable to process parameters: ");
            }

            var flights = new List<BaseAirportFlightModel>();

            if (disposition.Equals(DispositionFilter.None)) return flights;

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Apikey", _configuration["FlightAwareKey"]);
                var flightAwareQueryString = BuildFlightAwareQueryString(timeType, timeFrom, timeTo, timeAt, airline);
                var flightAwareResponseObject = await client.GetAsync("https://aeroapi.flightaware.com/aeroapi/airports/KBNA/flights/" + apiEndpoint + "?" + (string.IsNullOrWhiteSpace(flightAwareQueryString) ? "type=Airline" : flightAwareQueryString));
                var flightAwareResponseBody = flightAwareResponseObject.Content.ReadAsStringAsync().Result;

                if (flightAwareResponseBody == null) return flights;

                FlightAwareAirportFlightsResponseObject flightAwareResponse = JsonConvert.DeserializeObject<FlightAwareAirportFlightsResponseObject>(flightAwareResponseBody);
                
                if (flightAwareResponse != null)
                {
                    if (DispositionFilter.ScheduledArriving.Equals(disposition))
                    {
                        foreach (var arrival in flightAwareResponse.scheduled_arrivals)
                        {
                            flights.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Arrival,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares), 
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

                    if (DispositionFilter.Arrived.Equals(disposition))
                    {
                        foreach (var arrival in flightAwareResponse.arrivals)
                        {
                            flights.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Arrival,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
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

                    if (DispositionFilter.ScheduledDepartures.Equals(disposition))
                    {
                        foreach (var arrival in flightAwareResponse.scheduled_departures)
                        {
                            flights.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Departure,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_iata,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
                                ScheduledDepartureTime = arrival.scheduled_out,
                                ActualDepartureTime = arrival.actual_out,
                                ScheduledArrivalTime = arrival.scheduled_in,
                                ActualArrivalTime = arrival.actual_in,
                                CityCode = arrival.destination.code_iata,
                                CityName = arrival.destination.city,
                                CityAirportname = arrival.destination.name
                            });

                        }
                    }

                    if (DispositionFilter.Departed.Equals(disposition))
                    {
                        foreach (var arrival in flightAwareResponse.departures)
                        {
                            flights.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Departure,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_iata,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
                                ScheduledDepartureTime = arrival.scheduled_out,
                                ActualDepartureTime = arrival.actual_out,
                                ScheduledArrivalTime = arrival.scheduled_in,
                                ActualArrivalTime = arrival.actual_in,
                                CityCode = arrival.destination.code_iata,
                                CityName = arrival.destination.city,
                                CityAirportname = arrival.destination.name
                            });

                        }
                    }
                }

                return flights;
            }
        }

        private string BuildFlightAwareQueryString(
            TimeFilter timeType,
            DateTime start,
            DateTime end,
            DateTime at,
            string airline)
        {
            var queryStringList = new List<string>();

            if (TimeFilter.Between.Equals(timeType))
            {
                queryStringList.Add($"start={start:s}");
                queryStringList.Add($"end={end:s}");
            }
            else if (TimeFilter.At.Equals(timeType))
            {
                queryStringList.Add($"start={at:s}");
                queryStringList.Add($"end={at:s}");
            }

            if (!string.IsNullOrWhiteSpace(airline))
            {
                queryStringList.Add($"airline={airline}");
            }

            return string.Join("&", queryStringList);

        }

        private string GetAirlineWithCodesharePartners(string airline, List<string> codesharePartners)
        {
            try
            {
                var convertedAirline = GetAirline(airline);
                var convertedCodesharePartners = new List<string>();

                foreach (var codeshareParter in codesharePartners)
                {
                    foreach (var key in Airlines.Keys)
                    {
                        if (codeshareParter.StartsWith(key))
                        {
                            convertedCodesharePartners.Add(GetAirline(key));
                            
                        }
                    
                    }
                }

                return String.Join(" | ", new List<string>() { convertedAirline}.Concat(convertedCodesharePartners));

            }
            catch
            {
                return string.Empty;
            }

        }

        private string GetAirline(string airlineParm)
        {
            if (string.IsNullOrWhiteSpace(airlineParm)) return string.Empty;

            var airlineSearchKeyword = airlineParm.ToUpper().Replace(" ", "").Trim().Replace("AIRLINES", "").Replace("AIRWAYS", "").Replace("AIR", "");

            foreach (var key in Airlines.Keys)
            {
                if (key.StartsWith(airlineSearchKeyword)) return Airlines[key][0];

                var keywords = Airlines[key];

                foreach (var keyword in keywords)
                {
                    if (keyword.StartsWith(airlineSearchKeyword))
                    {
                        return key;
                    }
                }
            }

            return String.Empty;
        }

        private DateTime ParseDateFromString(string date)
        {
            DateTime result = new DateTime();
            DateTime.TryParse(date, out result);
            return result;
        }

        private DateTime GetToTime(DateTime? date = null)
        {
            var toDate = DateTime.Now;
            if (date.HasValue)
                toDate = date.Value;
            else
                date = DateTime.Now;

            toDate = toDate.AddHours(6);

            if (toDate.Day != date.Value.Day)
                toDate = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, 23, 59, 59);

            return toDate;
        }
    }
}