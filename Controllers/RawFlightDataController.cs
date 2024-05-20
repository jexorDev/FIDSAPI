using FIDSAPI.DataLayer.DataTransferObjects;
using FIDSAPI.DataLayer.SqlRepositories;
using FIDSAPI.Models;
using FIDSAPI.Models.FlightAware;
using FIDSAPI.Utility;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data.SqlClient;

namespace FIDSAPI.Controllers
{
    [ApiController]
    [Route("RawFlightData")]
    public class RawFlightDataController : ControllerBase
    {
        
        private readonly IConfiguration _configuration;
        private readonly ILogger<RawFlightDataController> _logger;
        private readonly FlightsSqlRepository _flightSqlRepository;

        public RawFlightDataController(ILogger<RawFlightDataController> logger, IConfiguration config)
        {
            _configuration = config;
            _logger = logger;
            _flightSqlRepository = new FlightsSqlRepository();
        }

        [HttpGet]
        public async Task<List<BaseAirportFlightModel>> Get([FromQuery] Enumerations.Disposition.Type dispositionType, DateTime fromDateTime, DateTime toDateTime, string? airline, string? city)
        {
            var flights = new List<BaseAirportFlightModel>();

            var airlineCode = string.Empty;

            if (!string.IsNullOrWhiteSpace(airline))
            {
                var convertedAirline = AirlineDirectory.GetAirlineByKeyword(airline);
                if (convertedAirline.HasValue)
                {
                    airlineCode = convertedAirline.Value.IATACode;
                }
            }

            using (SqlConnection connection = new SqlConnection(DatabaseConnectionStringBuilder.GetSqlConnectionString(_configuration)))
            {
                connection.Open();

                foreach (var flight in _flightSqlRepository.GetFlights(connection, dispositionType, fromDateTime, toDateTime, airlineCode, city))
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

                    flights.Add(flightModel);

                }

                connection.Close();
            }

            return flights;
        }

        [HttpPost]
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

                    var pk  = _flightSqlRepository.InsertFlight(flight, conn);
                    
                    foreach (var codesharePartner in arrival.codeshares)
                    {
                        _flightSqlRepository.InsertCodesharePartner(conn, pk, codesharePartner);
                    }
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

                    var pk = _flightSqlRepository.InsertFlight(flight, conn);

                    foreach (var codesharePartner in arrival.codeshares)
                    {
                        _flightSqlRepository.InsertCodesharePartner(conn, pk, codesharePartner);
                    }
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

                    var pk = _flightSqlRepository.InsertFlight(flight, conn);

                    foreach (var codesharePartner in departure.codeshares)
                    {
                        _flightSqlRepository.InsertCodesharePartner(conn, pk, codesharePartner);
                    }
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
                        CityName = departure.destination?.city ?? "",
                        CityAirportCode = departure.destination?.code_iata ?? "",
                        CityAirportName = departure.destination?.name ?? "",
                        DateTimeCreated = DateTime.UtcNow
                    };

                    var pk = _flightSqlRepository.InsertFlight(flight, conn);

                    foreach (var codesharePartner in departure.codeshares)
                    {
                        _flightSqlRepository.InsertCodesharePartner(conn, pk, codesharePartner);
                    }
                }
            }
        }
    }
}