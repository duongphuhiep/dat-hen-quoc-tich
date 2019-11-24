using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace nghe
{
    public class ViberAlertSender
    {
        private readonly HttpClient httpClient;

        public ViberAlertSender(string authToken)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Viber-Auth-Token", authToken);
        }

        private static readonly string API_ENDPOINT = "https://chatapi.viber.com/pa/broadcast_message";
        private static readonly string SENDER_NAME = "Crawler";
        private static readonly string SENDER_AVATAR = "https://share.cdn.viber.com/pg_download?id=0-04-01-eda6159d0a37b36731b47edeae7d6aa8cb3f8ec3be640b0c800a2c19998f1d32&filetype=jpg&type=icon";
		
        public async Task<string> Send(string message, IEnumerable<string> receiverIDs) 
        {
            //build request
            var reqObj = new {
                min_api_version = 1,
                sender = new {
                    name = SENDER_NAME,
                    avatar = SENDER_AVATAR
                },
                broadcast_list = receiverIDs,
                type = "text",
                text = message
            };
            var reqJson = JsonConvert.SerializeObject(reqObj);
            var req = new StringContent(reqJson, Encoding.UTF8, "application/json");

            var resp = await httpClient.PostAsync(API_ENDPOINT, req);

            var respStr = resp.IsSuccessStatusCode ? "OK" : "KO " + resp.StatusCode;
            if (resp.Content != null)
            {
                respStr += " " + await resp.Content.ReadAsStringAsync();
            }

            return respStr;
        }
    }
}
