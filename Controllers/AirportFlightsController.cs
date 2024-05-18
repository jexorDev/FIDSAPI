using FIDSAPI.DataLayer.DataTransferObjects;
using FIDSAPI.DataLayer.SqlRepositories;
using FIDSAPI.Enumerations;
using FIDSAPI.Models;
using FIDSAPI.Models.FlightAware;
using FIDSAPI.Utility;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data.SqlClient;
using System.Web;
using static FIDSAPI.Utility.AirlineDirectory;

namespace FIDSAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AirportFlightsController : ControllerBase
    {
        
        private readonly IConfiguration _configuration;
        private readonly ILogger<AirportFlightsController> _logger;
        private readonly FlightsSqlRepository _flightSqlRepository;            

        public AirportFlightsController(ILogger<AirportFlightsController> logger, IConfiguration config)
        {
            _configuration = config;
            _logger = logger;
            _flightSqlRepository = new FlightsSqlRepository();
        }

        //TODO: Move to new controller
        [HttpPost(Name = "PostPopulateCache")]
        public async Task PopulateCache()
        {
            using (SqlConnection connection = new SqlConnection(DatabaseConnectionStringBuilder.GetSqlConnectionString(_configuration)))
            {
                connection.Open();

                await PopulateFlightTable("arrivals", connection);
                Thread.Sleep(60000);
                await PopulateFlightTable("scheduled_arrivals", connection);
                Thread.Sleep(60000);
                await PopulateFlightTable("departures", connection);
                Thread.Sleep(60000);
                await PopulateFlightTable("scheduled_departures", connection);

                connection.Close();
            }
        }           

        [HttpGet(Name = "GetAirportFlights")]
        public async Task<GetResponseBody> Get([FromQuery] GetAirportFlightsRequestParameters parameters)
        {
            var nextDataPageUrlDecoded = HttpUtility.UrlDecode(parameters.NextDataPageUrl);

            Airline? airline = AirlineDirectory.GetAirlineByKeyword(parameters.Airline); 
            var apiEndpoint = string.Empty;
            
            //FlightAware API doesn't provide filtering by airport, so have to use cached data in db
            var useDatabaseCache = !string.IsNullOrWhiteSpace(parameters.Airport);

            if (Disposition.Type.ScheduledArriving.Equals(parameters.DispositionType))
            {
                apiEndpoint = "scheduled_arrivals";
            }
            else if (Disposition.Type.ScheduledDepartures.Equals(parameters.DispositionType))
            {
                apiEndpoint = "scheduled_departures";
            }
            else if (Disposition.Type.Arrived.Equals(parameters.DispositionType))
            {
                apiEndpoint = "arrivals";
            }
            else if (Disposition.Type.Departed.Equals(parameters.DispositionType))
            {
                apiEndpoint = "departures";
            }

            var response = new GetResponseBody();

            if (string.IsNullOrWhiteSpace(parameters.NextDataPageUrl) && parameters.DispositionType.Equals(Disposition.Type.None)) return response;

            if (useDatabaseCache)
            {
                using (SqlConnection connection = new SqlConnection(DatabaseConnectionStringBuilder.GetSqlConnectionString(_configuration)))
                {
                    connection.Open();

                    foreach (var flight in _flightSqlRepository.GetFlights(connection, parameters.DispositionType, parameters.DateTimeFrom, parameters.DateTimeTo, (airline.HasValue ? airline.Value.IATACode : ""), parameters.Airport))
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

                        var flightAirline = AirlineDirectory.GetAirlineByKeyword(flight.Airline);

                        if (flightAirline.HasValue)
                        {
                            flightModel.AirlineName = flightAirline.Value.Name;
                            flightModel.AirlineIdentifier = flightAirline.Value.ICAOCode;
                        }
                        else
                        {
                            flightModel.AirlineName = flight.Airline;
                            flightModel.AirlineIdentifier = flight.Airline;
                        }

                        response.Results.Add(flightModel);
                        
                    }

                    connection.Close();
                }                

                return response;
            }

            

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Apikey", _configuration["FlightAwareKey"]);
                string endpoint = string.Empty;

                if (string.IsNullOrWhiteSpace(parameters.NextDataPageUrl))
                {
                    endpoint = $"/airports/{_configuration["AirportCode"]}/flights/{apiEndpoint}?{FlightAwareApi.BuildFlightAwareQueryString(parameters.DateTimeFrom, parameters.DateTimeTo, (airline.HasValue ? airline.Value.ICAOCode : ""))}";
                }
                else
                {
                    endpoint = nextDataPageUrlDecoded;
                }

                var apiRequestQuery = $"{FlightAwareApi.BaseUri}{endpoint}";
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
                                Disposition = DispositionType.Arrival,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = FlightAwareApi.GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares), 
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
                                Disposition = DispositionType.Arrival,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_destination,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = FlightAwareApi.GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
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
                                Disposition = DispositionType.Departure,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_origin,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = FlightAwareApi.GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
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
                                Disposition = DispositionType.Departure,
                                FlightNumber = arrival.flight_number,
                                AirportGate = arrival.gate_origin,
                                AirlineIdentifier = arrival.operator_icao,
                                AirlineName = FlightAwareApi.GetAirlineWithCodesharePartners(arrival.operator_icao, arrival.codeshares),
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

        //TODO: move to new controller
        private async Task PopulateFlightTable(string resource, SqlConnection conn)
        {
            using (HttpClient client = new HttpClient())
            {
                int count = 0;
                var restOfUrl = string.Empty;
                var cursor = string.Empty;
                client.DefaultRequestHeaders.Add("X-Apikey", _configuration["FlightAwareKey"]);

                do
                {
                    if (string.IsNullOrWhiteSpace(cursor))
                    {
                        DateTime fromDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0).ToUniversalTime();
                        DateTime toDateTime = fromDateTime.AddHours(24).AddSeconds(-1);


                        restOfUrl = $"/airports/{_configuration["AirportCode"]}/flights/{resource}?type=Airline&start={DateConversions.GetFormattedISODateTime(fromDateTime)}&end={DateConversions.GetFormattedISODateTime(toDateTime)}";
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
                    var flightAwareResponseObject = await client.GetAsync(FlightAwareApi.BaseUri + restOfUrl);
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

                    _flightSqlRepository.InsertFlight(flight, conn);
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

                    _flightSqlRepository.InsertFlight(flight, conn);
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

                    _flightSqlRepository.InsertFlight(flight, conn);
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

                    _flightSqlRepository.InsertFlight(flight, conn);
                }
            }
        }             
    }
}