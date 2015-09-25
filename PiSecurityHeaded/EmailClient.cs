using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PiSecurityHeaded
{
    public sealed class EmailClient
    {
        const string DOMAIN = "sandbox3dda5b8dc9dd48cbbc402d3e4b38d126.mailgun.org";
        const string API_KEY = "key-b56695ac74d09597f2e001a4f3967cdf";

        public void SendMail(string email)
        {
            SendMailAsync(email).Wait();
        }

        private async Task SendMailAsync(string email)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes("api" + ":" + API_KEY)));


            var form = new Dictionary<string, string>();
            form["from"] = "intruders@gtfo.com";
            form["to"] = email;
            form["subject"] = "Intruder Alert";
            form["text"] = "Intruder detected at "+DateTime.Now.ToString();

            var response = await client.PostAsync("https://api.mailgun.net/v2/" + DOMAIN + "/messages", new FormUrlEncodedContent(form));

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Debug.WriteLine("Success");
            }
            else
            {
                Debug.WriteLine("StatusCode: " + response.StatusCode);
                Debug.WriteLine("ReasonPhrase: " + response.ReasonPhrase);
            }
        }
    }
}
