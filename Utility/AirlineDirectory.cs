namespace FIDSAPI.Utility
{
    public class AirlineDirectory
    {
        public struct Airline
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

        public static Dictionary<string, Airline> Airlines = new Dictionary<string, Airline>()
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
            {"RV", new Airline { Name = "AIR CANADA ROUGE", IATACode = "RV", ICAOCode = "ROU" } }, 
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

        public static Airline? GetAirlineByKeyword(string airlineKeyword)
        {
            if (string.IsNullOrWhiteSpace(airlineKeyword)) return null;

            airlineKeyword = airlineKeyword.ToUpper().Trim();

            if (Airlines.ContainsKey(airlineKeyword))
            {
                return Airlines[airlineKeyword];
            }
            
            foreach (string key in Airlines.Keys)
            {
                Airline airline = Airlines[key];

                if (string.Compare(airline.IATACode, airlineKeyword, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    return airline;
                }
                if (string.Compare(airline.ICAOCode, airlineKeyword, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    return airline;
                }
                if (string.Compare(airline.Name, airlineKeyword, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    return airline;
                }
            }

            return null;
            
        }
    }
}
