using Microsoft.AspNetCore.Mvc;
using FIDSAPI.Models;
using FIDSAPI.Models.FlightAware;
using Newtonsoft.Json;
using System.Data.SqlClient;
using FIDSAPI.Models.DataLayer;
using System.Web;

namespace FIDSAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AirportFlightsController : ControllerBase
    {
        
        private readonly IConfiguration _configuration;
        private readonly ILogger<AirportFlightsController> _logger;

        private struct Airline
        {
            /// <summary>
            /// User friendly display name of airline
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// IATA Code
            /// </summary>
            public string Code { get; set; }
            /// <summary>
            /// Repeat offender airline with routinely inconvenienced passengers
            /// </summary>
            public bool IsProblem { get; set; }
        }

        private Dictionary<string, List<string>> Airlines = new Dictionary<string, List<string>>()
        {
            //KEY: ICAO, VALUE: USER FRIENDLY, IATA
            {"AAY", new List<string> { "ALLEGIANT", "G4" } },
            {"AAL", new List<string> { "AMERICAN", "AA" } },
            {"ACA", new List<string> { "AIR CANADA", "AC" } },
            {"ASA", new List<string> { "ALASKA", "AS" } },
            {"ASH", new List<string> { "MESA", "YV" } },
            {"BAW", new List<string> { "BRITISH", "BA" } },
            {"DAL", new List<string> { "DELTA", "DL" } },
            {"EDV", new List<string> { "ENDEAVOR", "9E" } },
            {"ENY", new List<string> { "ENVOY", "MQ" } }, //AMERICAN AIRLINES
            {"FFT", new List<string> { "FRONTIER", "F9" } },
            {"FLE", new List<string> { "FLAIR", "F8" } },
            {"JBU", new List<string> { "JETBLUE", "B6" } },
            {"JIA", new List<string> { "PSA", "OH" } }, //AMERICAN AIRLINES
            {"ROU", new List<string> { "AIR CANADA ROUGE", "RV" } }, //AMERICAN AIRLINES
            {"RPA", new List<string> { "REPUBLIC", "YX" } }, 
            {"NKS", new List<string> { "SPIRIT", "NK" } },
            {"SCX", new List<string> { "SUNCOUNTRY", "SY" } },
            {"SKW", new List<string> { "SKYWEST", "OO" } },
            {"SWA", new List<string> { "SOUTHWEST", "WN" } },
            {"UAL", new List<string> { "UNITED", "UA" } },
            {"VIV", new List<string> { "VIVAAEROBUS", "VB" } },
            {"VTE", new List<string> { "CONTOUR", "LF" } },
            {"VXP", new List<string> { "AVELO", "XP" } },
            {"WJA", new List<string> { "WESTJET", "WS" } }
        };

        private Dictionary<string, Airline> Airlines2 = new Dictionary<string, Airline>()
        {
            {"AAY", new Airline { Name = "ALLEGIANT", Code = "G4", IsProblem = true } },
            {"AAL", new Airline { Name = "AMERICAN", Code = "AA" } },
            {"ACA", new Airline { Name = "AIR CANADA", Code = "AC" } },
            {"ASA", new Airline { Name = "ALASKA", Code = "AS" } },
            {"ASH", new Airline { Name = "MESA", Code = "YV" } },
            {"BAW", new Airline { Name = "BRITISH", Code = "BA" } },
            {"DAL", new Airline { Name = "DELTA", Code = "DL" } },
            {"EDV", new Airline { Name = "ENDEAVOR", Code = "9E" } },
            {"ENY", new Airline { Name = "ENVOY", Code = "MQ" } }, //AMERICAN AIRLINES
            {"FFT", new Airline { Name = "FRONTIER", Code = "F9", IsProblem = true } },
            {"FLE", new Airline { Name = "FLAIR", Code = "F8", IsProblem = true } },
            {"JBU", new Airline { Name = "JETBLUE", Code = "B6" } },
            {"JIA", new Airline { Name = "PSA", Code = "OH" } }, //AMERICAN AIRLINES
            {"ROU", new Airline { Name = "AIR CANADA ROUGE", Code = "RV" } }, //AMERICAN AIRLINES
            {"RPA", new Airline { Name = "REPUBLIC", Code = "YX" } },
            {"NKS", new Airline { Name = "SPIRIT", Code = "NK" } },
            {"SCX", new Airline { Name = "SUNCOUNTRY", Code = "SY", IsProblem = true } },
            {"SKW", new Airline { Name = "SKYWEST", Code = "OO" } },
            {"SWA", new Airline { Name = "SOUTHWEST", Code = "WN" } },
            {"UAL", new Airline { Name = "UNITED", Code = "UA" } },
            {"VIV", new Airline { Name = "VIVAAEROBUS", Code = "VB" } },
            {"VTE", new Airline { Name = "CONTOUR", Code = "LF" } },
            {"VXP", new Airline { Name = "AVELO", Code = "XP" } },
            {"WJA", new Airline { Name = "WESTJET", Code = "WS" } }
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

        public async Task GetFast()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = _configuration["FASTTDatabaseConnection_Server"]; 
            builder.Encrypt = true;

            builder.UserID = _configuration["FASTTDatabaseConnection_Username"];
            builder.Password = _configuration["FASTTDatabaseConnection_Password"];
            builder.InitialCatalog = _configuration["FASTTDatabaseConnection_Database"]; 

            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();

                await PopulateFlightTable("arrivals", connection);
                await PopulateFlightTable("scheduled_arrivals", connection);
                await PopulateFlightTable("departures", connection);
                await PopulateFlightTable("scheduled_departures", connection);

                //String sql = "SELECT * FROM flights";

                //using (SqlCommand command = new SqlCommand(sql, connection))
                //{
                //    using (SqlDataReader reader = command.ExecuteReader())
                //    {
                //        if (!reader.HasRows)
                //        {
                //            await PopulateFlightTable(connection);
                //        }


                //    }
                //}

                connection.Close();
            }
        }           

        [HttpGet(Name = "GetAirportFlights")]
        public async Task<GetResponseBody> Get([FromQuery] string parmString, [FromQuery] string? nextDataPageUrl)
        {
            var nextDataPageUrlDecoded = HttpUtility.UrlDecode(nextDataPageUrl);
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

            var response = new GetResponseBody();

            if (string.IsNullOrWhiteSpace(nextDataPageUrl) && disposition.Equals(DispositionFilter.None)) return response;


            const string BaseUri = "https://aeroapi.flightaware.com/aeroapi";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Apikey", _configuration["FlightAwareKey"]);
                string endpoint = string.Empty;

                if (string.IsNullOrWhiteSpace(nextDataPageUrl))
                {
                    endpoint = $"/airports/KBNA/flights/{apiEndpoint}?{BuildFlightAwareQueryString(timeType, timeFrom, timeTo, timeAt, airline)}";
                }
                else
                {
                    endpoint = nextDataPageUrlDecoded;
                }

                var flightAwareResponseObject = await client.GetAsync($"{BaseUri}{endpoint}");
                var flightAwareResponseBody = flightAwareResponseObject.Content.ReadAsStringAsync().Result;

                if (flightAwareResponseBody == null) return response;

                FlightAwareAirportFlightsResponseObject flightAwareResponse = JsonConvert.DeserializeObject<FlightAwareAirportFlightsResponseObject>(flightAwareResponseBody);
                
                if (flightAwareResponse != null)
                {
                    if (flightAwareResponse.scheduled_arrivals != null)
                    {
                        foreach (var arrival in flightAwareResponse.scheduled_arrivals)
                        {
                            response.Results.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Arrival,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares), 
                                ScheduledDepartureTime = arrival.scheduled_out,
                                EstimatedDepartureTime = arrival.estimated_out,
                                ActualDepartureTime = arrival.actual_out,
                                ScheduledArrivalTime = arrival.scheduled_in,
                                EstimatedArrivalTime = arrival.estimated_in,
                                ActualArrivalTime = arrival.actual_in,
                                CityCode = arrival.origin.code_iata,
                                CityName = arrival.origin.city,
                                CityAirportName = arrival.origin.name,
                                RawData = flightAwareResponseBody
                            });
                        }
                    }

                    if (flightAwareResponse.arrivals != null)
                    {
                        foreach (var arrival in flightAwareResponse.arrivals)
                        {
                            response.Results.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Arrival,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
                                ScheduledDepartureTime = arrival.scheduled_out,
                                EstimatedDepartureTime = arrival.estimated_out,
                                ActualDepartureTime = arrival.actual_out,
                                ScheduledArrivalTime = arrival.scheduled_in,
                                EstimatedArrivalTime = arrival.estimated_in,
                                ActualArrivalTime = arrival.actual_in,
                                CityCode = arrival.origin.code_iata,
                                CityName = arrival.origin.city,
                                CityAirportName = arrival.origin.name,
                                RawData = flightAwareResponseBody
                            });
                        }
                    }

                    if (flightAwareResponse.scheduled_departures != null)
                    {
                        foreach (var arrival in flightAwareResponse.scheduled_departures)
                        {
                            response.Results.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Departure,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_origin,
                                AirlineIdentifier = arrival.operator_iata,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
                                ScheduledDepartureTime = arrival.scheduled_out,
                                EstimatedDepartureTime = arrival.estimated_out,
                                ActualDepartureTime = arrival.actual_out,
                                ScheduledArrivalTime = arrival.scheduled_in,
                                EstimatedArrivalTime = arrival.estimated_in,
                                ActualArrivalTime = arrival.actual_in,
                                CityCode = arrival.destination.code_iata,
                                CityName = arrival.destination.city,
                                CityAirportName = arrival.destination.name,
                                RawData = flightAwareResponseBody
                            });

                        }
                    }

                    if (flightAwareResponse.departures != null)
                    {
                        foreach (var arrival in flightAwareResponse.departures)
                        {
                            response.Results.Add(new BaseAirportFlightModel
                            {
                                Status = arrival.status,
                                Disposition = Disposition.Departure,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_origin,
                                AirlineIdentifier = arrival.operator_iata,
                                AirlineName = GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
                                ScheduledDepartureTime = arrival.scheduled_out,
                                EstimatedDepartureTime = arrival.estimated_out,
                                ActualDepartureTime = arrival.actual_out,
                                ScheduledArrivalTime = arrival.scheduled_in,
                                EstimatedArrivalTime = arrival.estimated_in,
                                ActualArrivalTime = arrival.actual_in,
                                CityCode = arrival.destination.code_iata,
                                CityName = arrival.destination.city,
                                CityAirportName = arrival.destination.name,
                                RawData = flightAwareResponseBody
                            });

                        }
                    }

                    response.NextDataPageUrl = HttpUtility.UrlEncode(flightAwareResponse?.links?.next);
                }

               

                return response;
            }
        }

        private async Task PopulateFlightTable(string resource, SqlConnection conn)
        {

            using (HttpClient client = new HttpClient())
            {
                int count = 0;
                const string BaseUrl = "https://aeroapi.flightaware.com/aeroapi";
                var restOfUrl = string.Empty;
                var cursor = string.Empty;
                client.DefaultRequestHeaders.Add("X-Apikey", _configuration["FlightAwareKey"]);

                do
                {
                    if (string.IsNullOrWhiteSpace(cursor))
                    {
                        DateTime currentUtcDate = DateTime.UtcNow;
                        DateTime fromDateTime = currentUtcDate.AddHours(-currentUtcDate.Hour).AddMinutes(-currentUtcDate.Minute).AddSeconds(-currentUtcDate.Second).AddHours(-5);
                        DateTime toDateTime = fromDateTime.AddHours(24).AddSeconds(-1);


                        restOfUrl = $"/airports/KBNA/flights/{resource}?type=Airline&start={GetFormattedISODateTime(fromDateTime)}&end={GetFormattedISODateTime(toDateTime)}";
                    }
                    else
                    {
                        restOfUrl = cursor;
                    }

                    if (count > 9)
                    {
                        Thread.Sleep(60000);
                        count = 0;
                    }
                    var flightAwareResponseObject = await client.GetAsync(BaseUrl + restOfUrl);
                    count++; 
                    var flightAwareResponseBody = flightAwareResponseObject.Content.ReadAsStringAsync().Result;

                    if (flightAwareResponseBody == null) return;

                    FlightAwareAirportFlightsResponseObject flightAwareResponse = JsonConvert.DeserializeObject<FlightAwareAirportFlightsResponseObject>(flightAwareResponseBody);

                    if (flightAwareResponse != null)
                    {
                        InsertFlights(flightAwareResponse, conn);
                    }

                    cursor = flightAwareResponse?.links?.next;

                } while (!string.IsNullOrWhiteSpace(cursor));
            }
        }

        private string GetFormattedISODateTime (DateTime dateTime)
        {
            
            return $"{dateTime.Year}-{dateTime.Month.ToString().PadLeft(2, '0')}-{dateTime.Day.ToString().PadLeft(2, '0')}T{dateTime.Hour.ToString().PadLeft(2, '0')}:{dateTime.Minute.ToString().PadLeft(2, '0')}:{dateTime.Second.ToString().PadLeft(2, '0')}Z";
        }

        private void InsertFlights(FlightAwareAirportFlightsResponseObject flightAwareResponse, SqlConnection conn)
        {
            if (flightAwareResponse.arrivals != null)
            {
                foreach (var arrival in flightAwareResponse.arrivals)
                {
                    var flight = new Flight
                    {
                        Disposition = true,
                        FlightNumber = arrival.flight_number,
                        Airline = arrival.operator_iata,
                        DateTimeScheduled = arrival.scheduled_in,
                        DateTimeEstimated = arrival.estimated_in,
                        DateTimeActual = arrival.actual_in,
                        Gate = arrival.gate_destination,
                        CityName = arrival.origin.city,
                        CityAirportCode = arrival.origin.code_iata,
                        CityAirportName = arrival.origin.name,
                        DateTimeCreated = DateTime.UtcNow
                    };

                    InsertFlight(flight, conn);
                }
            }

            
            if (flightAwareResponse.scheduled_arrivals != null)
            {
                foreach (var arrival in flightAwareResponse.scheduled_arrivals)
                {
                    var flight = new Flight
                    {
                        Disposition = true,
                        FlightNumber = arrival.flight_number,
                        Airline = arrival.operator_iata,
                        DateTimeScheduled = arrival.scheduled_in,
                        DateTimeEstimated = arrival.estimated_in,
                        DateTimeActual = arrival.actual_in,
                        Gate = arrival.gate_destination,
                        CityName = arrival.origin.city,
                        CityAirportCode = arrival.origin.code_iata,
                        CityAirportName = arrival.origin.name,
                        DateTimeCreated = DateTime.UtcNow
                    };

                    InsertFlight(flight, conn);
                }
            }

            if (flightAwareResponse.departures != null)
            {
                foreach (var departure in flightAwareResponse.departures)
                {
                    var flight = new Flight
                    {
                        Disposition = false,
                        FlightNumber = departure.flight_number,
                        Airline = departure.operator_iata,
                        DateTimeScheduled = departure.scheduled_out,
                        DateTimeEstimated = departure.estimated_out,
                        DateTimeActual = departure.actual_out,
                        Gate = departure.gate_destination,
                        CityName = departure.destination.city,
                        CityAirportCode = departure.destination.code_iata,
                        CityAirportName = departure.destination.name,
                        DateTimeCreated = DateTime.UtcNow
                    };

                    InsertFlight(flight, conn);
                }
            }

            if (flightAwareResponse.scheduled_departures != null)
            {
                foreach (var departure in flightAwareResponse.scheduled_departures)
                {
                    var flight = new Flight
                    {
                        Disposition = false,
                        FlightNumber = departure.flight_number,
                        Airline = departure.operator_iata,
                        DateTimeScheduled = departure.scheduled_out,
                        DateTimeEstimated = departure.estimated_out,
                        DateTimeActual = departure.actual_out,
                        Gate = departure.gate_destination,
                        CityName = departure.destination.city,
                        CityAirportCode = departure.destination.code_iata,
                        CityAirportName = departure.destination.name,
                        DateTimeCreated = DateTime.UtcNow
                    };

                    InsertFlight(flight, conn);
                }
            }
        }

        private void InsertFlight(Flight flight, SqlConnection conn)
        {
            string sql = @"
INSERT INTO Flights 
(
 Disposition
,FlightNumber
,Airline
,DateTimeScheduled
,Gate
,CityName
,CityAirportName
,CityAirportCode
,DateTimeCreated
)
VALUES
(
 @Disposition
,@FlightNumber
,@Airline
,@DateTimeScheduled
,@Gate
,@CityName
,@CityAirportName
,@CityAirportCode
,@DateTimeCreated
)
";
            using (SqlCommand command = new SqlCommand(sql, conn))
            {
                command.Parameters.AddWithValue("@Disposition", flight.Disposition);
                command.Parameters.AddWithValue("@FlightNumber", flight.FlightNumber ?? "");
                command.Parameters.AddWithValue("@Airline", flight.Airline ?? "");
                if (flight.DateTimeScheduled.HasValue)
                {
                    command.Parameters.AddWithValue("@DateTimeScheduled", flight.DateTimeScheduled.Value);

                }
                else
                {
                    command.Parameters.AddWithValue("@DateTimeScheduled", DBNull.Value);

                }
                command.Parameters.AddWithValue("@Gate", flight.Gate ?? "");
                command.Parameters.AddWithValue("@CityName", flight.CityName ?? "");
                command.Parameters.AddWithValue("@CityAirportName", flight.CityAirportName ?? "");
                command.Parameters.AddWithValue("@CityAirportCode", flight.CityAirportCode ?? "");
                command.Parameters.AddWithValue("@DateTimeCreated", DateTime.UtcNow);

                command.ExecuteNonQuery();
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
                queryStringList.Add($"start={start.AddHours(5):s}");
                queryStringList.Add($"end={end.AddHours(5):s}");
            }
            else if (TimeFilter.At.Equals(timeType))
            {
                queryStringList.Add($"start={at.AddHours(5).AddMinutes(-30):s}");
                queryStringList.Add($"end={at.AddHours(5).AddMinutes(30):s}");
            }

            if (!string.IsNullOrWhiteSpace(airline))
            {
                queryStringList.Add($"airline={airline}");
            }
            else
            {
                queryStringList.Add("type=Airline");
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