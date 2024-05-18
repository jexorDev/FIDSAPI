using FIDSAPI.DataLayer.DataTransferObjects;
using FIDSAPI.Enumerations;
using System.Data.SqlClient;

namespace FIDSAPI.DataLayer.SqlRepositories
{
    public class FlightsSqlRepository
    {
        public List<Flight> GetFlights(SqlConnection conn, Disposition.Type disposition, DateTime fromDate, DateTime toDate, string airline, string city)
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
                command.Parameters.AddWithValue("@FromDate",fromDate);
                command.Parameters.AddWithValue("@ToDate", toDate);
                filterString = "WHERE DateTimeScheduled BETWEEN @FromDate AND @ToDate ";

                if (Disposition.Type.ScheduledArriving.Equals(disposition) || Disposition.Type.Arrived.Equals(disposition))
                {
                    command.Parameters.AddWithValue("@Disposition", 1);
                    filterString += "AND Disposition = @Disposition ";
                }
                else if (Disposition.Type.ScheduledDepartures.Equals(disposition) || Disposition.Type.Departed.Equals(disposition))
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

        public void InsertFlight(Flight flight, SqlConnection conn)
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
    }
}
