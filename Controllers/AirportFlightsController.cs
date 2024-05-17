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
            public string IATACode { get; set; }
            /// <summary>
            /// ICAO Code
            /// </summary>
            public string ICAOCode { get; set; }
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
            {"G4", new Airline { Name = "ALLEGIANT", IATACode = "G4", ICAOCode = "AAY", IsProblem = true } },
            {"AA", new Airline { Name = "AMERICAN", IATACode = "AA", ICAOCode = "AAL" } },
            {"AC", new Airline { Name = "AIR CANADA", IATACode = "AC", ICAOCode = "ACA" } },
            {"AS", new Airline { Name = "ALASKA", IATACode = "AS", ICAOCode = "ASA" } },
            {"YV", new Airline { Name = "MESA", IATACode = "YV", ICAOCode = "ASH" } },
            {"BA", new Airline { Name = "BRITISH", IATACode = "BA", ICAOCode = "BAW" } },
            {"DL", new Airline { Name = "DELTA", IATACode = "DL", ICAOCode = "DAL" } },
            {"9E", new Airline { Name = "ENDEAVOR", IATACode = "9E", ICAOCode = "EDV" } },
            {"MQ", new Airline { Name = "ENVOY", IATACode = "MQ", ICAOCode = "ENY" } }, //AMERICAN AIRLINES
            {"F9", new Airline { Name = "FRONTIER", IATACode = "F9", ICAOCode = "FFT", IsProblem = true } },
            {"F8", new Airline { Name = "FLAIR", IATACode = "F8", ICAOCode = "FLE", IsProblem = true } },
            {"B6", new Airline { Name = "JETBLUE", IATACode = "B6", ICAOCode = "JBU" } },
            {"OH", new Airline { Name = "PSA", IATACode = "OH", ICAOCode = "JIA" } }, //AMERICAN AIRLINES
            {"RV", new Airline { Name = "AIR CANADA ROUGE", IATACode = "RV", ICAOCode = "ROU" } }, //AMERICAN AIRLINES
            {"YX", new Airline { Name = "REPUBLIC", IATACode = "YX", ICAOCode = "RPA" } },
            {"NK", new Airline { Name = "SPIRIT", IATACode = "NK", ICAOCode = "NKS" } },
            {"SY", new Airline { Name = "SUNCOUNTRY", IATACode = "SY", ICAOCode = "SCX", IsProblem = true } },
            {"OO", new Airline { Name = "SKYWEST", IATACode = "OO", ICAOCode = "SKW" } },
            {"WN", new Airline { Name = "SOUTHWEST", IATACode = "WN", ICAOCode = "SWA" } },
            {"UA", new Airline { Name = "UNITED", IATACode = "UA", ICAOCode = "UAL" } },
            {"VB", new Airline { Name = "VIVAAEROBUS", IATACode = "VB", ICAOCode = "VIV" } },
            {"LF", new Airline { Name = "CONTOUR", IATACode = "LF", ICAOCode = "VTE" } },
            {"XP", new Airline { Name = "AVELO", IATACode = "XP", ICAOCode = "VXP", IsProblem = true } },
            {"WS", new Airline { Name = "WESTJET", IATACode = "WS", ICAOCode = "WJA" } }
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
            None,
            Between,
            At
        }

        public AirportFlightsController(ILogger<AirportFlightsController> logger, IConfiguration config)
        {
            _configuration = config;
            _logger = logger;
        }

        public class FastDatabaseConnectionStringBuilder
        {
            private static SqlConnectionStringBuilder? _sqlConnectionStringBuilder;
            public static string GetSqlConnectionString(IConfiguration config)
            {
                if (_sqlConnectionStringBuilder == null)
                {
                    _sqlConnectionStringBuilder = new SqlConnectionStringBuilder();

                    _sqlConnectionStringBuilder.Encrypt = true;

                    _sqlConnectionStringBuilder.DataSource = config["FASTTDatabaseConnection_Server"];
                    _sqlConnectionStringBuilder.UserID = config["FASTTDatabaseConnection_Username"];
                    _sqlConnectionStringBuilder.Password = config["FASTTDatabaseConnection_Password"];
                    _sqlConnectionStringBuilder.InitialCatalog = config["FASTTDatabaseConnection_Database"];
                }

                return _sqlConnectionStringBuilder.ToString();
            }

        }

        [HttpPost(Name = "PostPopulateCache")]
        public async Task PopulateCache()
        {


            using (SqlConnection connection = new SqlConnection(FastDatabaseConnectionStringBuilder.GetSqlConnectionString(_configuration)))
            {
                connection.Open();

                await PopulateFlightTable("arrivals", connection);
                Thread.Sleep(60000);
                await PopulateFlightTable("scheduled_arrivals", connection);
                Thread.Sleep(60000);
                await PopulateFlightTable("departures", connection);
                Thread.Sleep(60000);
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
            TimeFilter timeType = TimeFilter.None;
            var timeFrom = DateTime.Now;
            var timeTo = GetToTime();
            var timeAt = DateTime.Now;
            var airlineName = string.Empty;
            string rawAirlineParm = string.Empty;
            var airport = string.Empty;
            var apiEndpoint = string.Empty;
            var useDatabaseCache = false;

            try
            {
                foreach (var parm in parmsList)
                {
                    if (parm.StartsWith("arriving"))
                    {
                        disposition = DispositionFilter.ScheduledArriving;
                        var parms = parm.Split(" ");
                        rawAirlineParm = parms[1];
                        airlineName = GetAirline(parms[1]);
                        apiEndpoint = "scheduled_arrivals";
                    }
                    else if (parm.StartsWith("departing"))
                    {
                        disposition = DispositionFilter.ScheduledDepartures;
                        var parms = parm.Split(" ");
                        rawAirlineParm = parms[1];
                        airlineName = GetAirline(parms[1]);
                        apiEndpoint = "scheduled_departures";
                    }
                    else if (parm.StartsWith("arrived"))
                    {
                        disposition = DispositionFilter.Arrived;
                        var parms = parm.Split(" ");
                        rawAirlineParm = parms[1];
                        airlineName = GetAirline(parms[1]);
                        apiEndpoint = "arrivals";
                    }
                    else if (parm.StartsWith("departed"))
                    {
                        disposition = DispositionFilter.Departed;
                        var parms = parm.Split(" ");
                        rawAirlineParm = parms[1];
                        airlineName = GetAirline(parms[1]);
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

                    if (parm.StartsWith("from") || parm.StartsWith("to"))
                    {
                        useDatabaseCache = true;
                        var parms = parm.Split(" ");
                        airport = parms[1];
                    }
                }

            }
            catch (Exception ex)
            {
                //return BadRequest("Unable to process parameters: ");
            }

            var response = new GetResponseBody();

            if (string.IsNullOrWhiteSpace(nextDataPageUrl) && disposition.Equals(DispositionFilter.None)) return response;

            if (TimeFilter.None.Equals(timeType))
            {
                timeType = TimeFilter.Between;
                timeFrom = DateTime.Now.AddHours(-DateTime.Now.Hour).AddMinutes(-DateTime.Now.Minute).AddSeconds(-DateTime.Now.Second);
                timeTo = timeFrom.AddHours(24);
            }

            if (useDatabaseCache)
            {
                using (SqlConnection connection = new SqlConnection(FastDatabaseConnectionStringBuilder.GetSqlConnectionString(_configuration)))
                {
                    connection.Open();

                    foreach (var flight in GetFlights(connection, disposition, timeFrom, timeTo, rawAirlineParm, airport))
                    {
                        var flightModel = new BaseAirportFlightModel
                        {
                            FlightNumber = flight.FlightNumber,
                            AirportGate = flight.Gate,
                            ScheduledArrivalTime = flight.DateTimeScheduled,
                            ScheduledDepartureTime = flight.DateTimeScheduled,
                            EstimatedArrivalTime = flight.DateTimeEstimated,
                            EstimatedDepartureTime = flight.DateTimeEstimated,
                            ActualArrivalTime = flight.DateTimeActual,
                            ActualDepartureTime = flight.DateTimeActual,
                            CityName = flight.CityName,
                            CityCode = flight.CityAirportCode,
                            CityAirportName = flight.CityAirportName
                        };

                        if (!string.IsNullOrWhiteSpace(flight.Airline) && Airlines2.ContainsKey(flight.Airline))
                        {
                            flightModel.AirlineName = Airlines2[flight.Airline].Name;
                            flightModel.AirlineIdentifier = Airlines2[flight.Airline].ICAOCode;
                        }
                        else
                        {
                            flightModel.AirlineName = GetAirline(flight.Airline);
                            flightModel.AirlineIdentifier = flight.Airline;
                        }

                        response.Results.Add(flightModel);
                        
                    }

                    connection.Close();
                }                

                return response;
            }

            const string BaseUri = "https://aeroapi.flightaware.com/aeroapi";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Apikey", _configuration["FlightAwareKey"]);
                string endpoint = string.Empty;

                if (string.IsNullOrWhiteSpace(nextDataPageUrl))
                {
                    endpoint = $"/airports/KBNA/flights/{apiEndpoint}?{BuildFlightAwareQueryString(timeType, timeFrom, timeTo, timeAt, airlineName)}";
                }
                else
                {
                    endpoint = nextDataPageUrlDecoded;
                }

                var apiRequestQuery = $"{BaseUri}{endpoint}";
                var flightAwareResponseObject = await client.GetAsync(apiRequestQuery);
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
                                AirlineIdentifier = arrival.operator_icao,
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
                                AirlineIdentifier = arrival.operator_icao,
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
                            });

                        }
                    }

                    response.NextDataPageUrl = HttpUtility.UrlEncode(flightAwareResponse?.links?.next);
                }


                response.GeneratedApiQuery = apiRequestQuery;
                response.RawData = flightAwareResponseBody;

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
                        DateTime fromDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0).ToUniversalTime();
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

        //TODO: MOVE TO DATALAYER
        private List<Flight> GetFlights(SqlConnection conn, DispositionFilter disposition, DateTime fromDate, DateTime toDate, string airline, string city)
        {
            var flights = new List<Flight>();
            string sql = @"
SELECT 
 Disposition
,FlightNumber
,Airline
,DateTimeScheduled
,DateTimeEstimated
,DateTimeActual
,Gate
,CityName
,CityAirportName
,CityAirportCode
,DateTimeCreated
FROM Flights
";
            var filterString = string.Empty;
            using (SqlCommand command = new SqlCommand(sql, conn))
            {
                command.Parameters.AddWithValue("@FromDate", ConvertDateTimeToUtc(fromDate));
                command.Parameters.AddWithValue("@ToDate", ConvertDateTimeToUtc(toDate));
                filterString = "WHERE DateTimeScheduled BETWEEN @FromDate AND @ToDate ";

                if (DispositionFilter.ScheduledArriving.Equals(disposition) || DispositionFilter.Arrived.Equals(disposition))
                {
                    command.Parameters.AddWithValue("@Disposition", 1);
                    filterString += "AND Disposition = @Disposition ";
                }
                else if (DispositionFilter.ScheduledDepartures.Equals(disposition) || DispositionFilter.Departed.Equals(disposition))
                {
                    command.Parameters.AddWithValue("@Disposition", 0);
                    filterString += "AND Disposition = @Disposition ";
                }

                if (!string.IsNullOrWhiteSpace(airline))
                {
                    command.Parameters.AddWithValue("@Airline", airline);
                    filterString += "AND Airline = @Airline ";
                }
               
                if (!string.IsNullOrWhiteSpace(city))
                {
                    command.Parameters.AddWithValue("@CityAirportCode", city);
                    filterString += "AND CityAirportCode = @CityAirportCode ";
                }

                command.CommandText += filterString;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var flight = new Flight
                        {
                            Disposition = bool.Parse(reader["Disposition"].ToString()),
                            FlightNumber = reader["FlightNumber"].ToString().Trim(),
                            Airline = reader["Airline"].ToString().Trim(),
                            Gate = reader["Gate"].ToString(),
                            CityName = reader["CityName"].ToString(),
                            CityAirportName = reader["CityAirportName"].ToString(),
                            CityAirportCode = reader["CityAirportCode"].ToString(),
                            DateTimeCreated = DateTime.Parse(reader["DateTimeCreated"].ToString())
                        };

                        DateTime parsedTime;

                        if (DateTime.TryParse(reader["DateTimeScheduled"].ToString(), out parsedTime))
                        {
                            //TODO: Why is the to local time needed when it's not when pulling directly from the FA API?
                            flight.DateTimeScheduled = parsedTime.ToLocalTime();
                        }
                        if (DateTime.TryParse(reader["DateTimeEstimated"].ToString(), out parsedTime))
                        {
                            flight.DateTimeEstimated = parsedTime.ToLocalTime();
                        }
                        if (DateTime.TryParse(reader["DateTimeActual"].ToString(), out parsedTime))
                        {
                            flight.DateTimeActual = parsedTime.ToLocalTime();
                        }

                        flights.Add(flight);
                    }
                }
            }

            return flights;
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
,DateTimeEstimated
,DateTimeActual
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
,@DateTimeEstimated
,@DateTimeActual
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
                
                if (flight.DateTimeEstimated.HasValue)
                {
                    command.Parameters.AddWithValue("@DateTimeEstimated", flight.DateTimeEstimated.Value);

                }
                else
                {
                    command.Parameters.AddWithValue("@DateTimeEstimated", DBNull.Value);

                }
                
                if (flight.DateTimeActual.HasValue)
                {
                    command.Parameters.AddWithValue("@DateTimeActual", flight.DateTimeActual.Value);

                }
                else
                {
                    command.Parameters.AddWithValue("@DateTimeActual", DBNull.Value);

                }
                command.Parameters.AddWithValue("@Gate", flight.Gate ?? "");
                command.Parameters.AddWithValue("@CityName", flight.CityName ?? "");
                command.Parameters.AddWithValue("@CityAirportName", flight.CityAirportName ?? "");
                command.Parameters.AddWithValue("@CityAirportCode", flight.CityAirportCode ?? "");
                command.Parameters.AddWithValue("@DateTimeCreated", DateTime.Now);

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
                queryStringList.Add($"start={ConvertDateTimeToUtc(start):s}");
                queryStringList.Add($"end={ConvertDateTimeToUtc(end):s}");
            }
            else if (TimeFilter.At.Equals(timeType))
            {
                queryStringList.Add($"start={ConvertDateTimeToUtc(at).AddMinutes(-30):s}");
                queryStringList.Add($"end={ConvertDateTimeToUtc(at).AddMinutes(30):s}");
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

        private DateTime ConvertDateTimeToUtc(DateTime dateToConvert)
        {
            TimeZoneInfo kstZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            return TimeZoneInfo.ConvertTime(dateToConvert, kstZone).ToUniversalTime();
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