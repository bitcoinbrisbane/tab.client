using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using tab.client.Models.Race;
using System.Linq;

namespace tab.client
{
    public class TabClient : IHorseRaceClient, IAuthenticationClient, IDisposable
    {
        private readonly Guid _transactionID;

        public String Token { get; set; }

        public Int32 AccountNumber { get; set; }

        public String CustomerNumber { get; private set; }

        private readonly string _jurisdiction = "QLD";

        private HttpClient _client = new HttpClient();

        public TabClient()
        {
        }

        public TabClient(String token)
        {
            Token = token;
        }

        public TabClient(Guid transactionID, String token)
        {
            _transactionID = transactionID;
            Token = token;
        }

        public async Task Authenticate(Int32 accountNumber, String password)
        {
            this.AccountNumber = accountNumber;

            using (HttpClient client = new HttpClient())
            {
                tab.client.Models.Authentication.Request request = new Models.Authentication.Request()
                {
                    AccountNumber = this.AccountNumber,
                    Password = password,
                    TmxSession = Guid.NewGuid(),
                    Channel = "TABCOMAU",
                    ExtendedTokenLifeTime = true
                };

                const String url = "https://webapi.tab.com.au/v1/account-service/tab/authenticate";
                String jsonRequest = JsonConvert.SerializeObject(request);
                StringContent content = new StringContent(jsonRequest, UnicodeEncoding.UTF8, "application/json");

                var result = await client.PostAsync(url, content);
                var json = await result.Content.ReadAsStringAsync();

                var response = JsonConvert.DeserializeObject<Models.Authentication.Response>(json);

                this.Token = response.authentication.token;
            }
        }

        public async Task<Models.Bet.Response> Bet(IEnumerable<Models.Bet.Bet> bets)
        {
            using (HttpClient client = new HttpClient())
            {
                tab.client.Models.Bet.Request request = new Models.Bet.Request();
                request.Bets.AddRange(bets);

                String url = String.Format("https://webapi.tab.com.au/v1/tab-betting-service/accounts/{0}/betslip?TabcorpAuth={1}", this.AccountNumber, this.Token);
                String jsonRequest = JsonConvert.SerializeObject(request);
                StringContent content = new StringContent(jsonRequest, UnicodeEncoding.UTF8, "application/json");

                var result = await client.PostAsync(url, content);
                var json = await result.Content.ReadAsStringAsync();

                var betResponse = JsonConvert.DeserializeObject<Models.Bet.Response>(json);
                return betResponse;
            }
        }

        public async Task Enquire(List<Models.Authentication.Betting> bets)
        {
            if (String.IsNullOrEmpty(Token))
            {
                throw new ArgumentNullException("Token must be set.");
            }

            using (HttpClient client = new HttpClient())
            {
                String url = String.Format("https://webapi.tab.com.au/v1/tab-betting-service/accounts/{0}/betslip-enquiry?TabcorpAuth={1}", AccountNumber, Token);
                
                using (var content = new StringContent(JsonConvert.SerializeObject(bets), System.Text.Encoding.UTF8, "application/json"))
                {
                    var response = await client.PostAsync(url, content);
                    var json = await response.Content.ReadAsStringAsync();


                    // List<Meet> meetings = new List<Meet>();
                    // foreach(Models.TAB.Meeting meeting in meetingResponse.Meetings)
                    // {
                    //     meetings.Add(new Meet() { Location = meeting.Location, Name = meeting.MeetingName });
                    // }
                }
            }
        }

        public async Task<IEnumerable<Models.Meeting.Meeting>> GetMeets(DateTime date)
        {
            using (HttpClient client = new HttpClient())
            {
                String url = String.Format("https://api.beta.tab.com.au/v1/tab-info-service/racing/dates/{0:yyyy-MM-dd}/meetings?jurisdiction=QLD", date);
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                var meetingResponse = JsonConvert.DeserializeObject<Models.Meeting.Response>(json);
                return meetingResponse.Meetings.Where(x => x.RaceType == "R");
            }
        }

        public async Task<Models.Races.Response> GetTodaysRaces()
        {
            using (HttpClient client = new HttpClient())
            {
                String url = String.Format("https://api.beta.tab.com.au/v1/tab-info-service/racing/next-to-go/races?includeFixedOdds=true&returnPromo=false&returnOffers=false&jurisdiction={0}", _jurisdiction);
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                Models.Races.Response racesResponse = JsonConvert.DeserializeObject<Models.Races.Response>(json);
                return racesResponse;
            }
        }

        public async Task<Models.Race.Response> GetRace(DateTime date, String location, Int32 racenumber)
        {
            //WHERE LOCATION IS MNEMONIC
            using (HttpClient client = new HttpClient())
            {
                String url = String.Format("https://api.beta.tab.com.au/v1/tab-info-service/racing/dates/{0:yyyy-MM-dd}/meetings/R/{1}/races/{2}?returnPromo=false&returnOffers=false&jurisdiction={3}", date, location, racenumber, _jurisdiction);
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                Models.Race.Response raceResponse = JsonConvert.DeserializeObject<Models.Race.Response>(json);
                return raceResponse;
            }
        }

        public async Task<Models.Runner.Response> GetRunners(DateTime date, string location, int number)
        {
            try
            {
                //WHERE LOCATION IS MNEMONIC
                using (HttpClient client = new HttpClient())
                {
                    String url = String.Format("https://api.beta.tab.com.au/v1/tab-info-service/racing/dates/{0:yyyy-MM-dd}/meetings/R/{1}/races/{2}?returnPromo=false&returnOffers=false&jurisdiction=QLD", date, location, number);
                    var response = await client.GetAsync(url);
                    var json = await response.Content.ReadAsStringAsync();

                    var runnerResponse = JsonConvert.DeserializeObject<Models.Runner.Response>(json);
                    
                    return runnerResponse;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
        }

        public async Task<tab.client.Models.Transactions.Response> GetTransactions(DateTime from, DateTime to)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("TabcorpAuth", Token);
                String url = String.Format("https://webapi.tab.com.au/v1/account-service/tab/accounts/{0}/transactional-records?fromDate={1:yyyy-MM-dd}&toDate={2:yyyy-MM-dd}", AccountNumber, from, to);
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                var transactionResponse = JsonConvert.DeserializeObject<tab.client.Models.Transactions.Response>(json);
                
                return transactionResponse;
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}