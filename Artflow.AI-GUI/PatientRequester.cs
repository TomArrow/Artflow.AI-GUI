using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Artflow.AI_GUI
{
    class PatientRequester
    {
        private static readonly HttpClient client = new HttpClient();
        private const int RETRYCOUNT = 20;

        public struct Response {
            public bool success;
            public byte[] rawData;
            public string responseAsString;
            public string headerContentType;
            //public string headerContentEncoding;
            public long? headerContentLength;
            //public Dictionary<string, string> responseHeaders;
        }

        public async static Task<Response> post(string url, Dictionary<string,string> postData)
        {
            Response retVal = new Response();

            FormUrlEncodedContent content = new FormUrlEncodedContent(postData);

            bool success = false;
            int errorCount = 0;

            while (!success && errorCount < RETRYCOUNT)
            {
                try
                {

                    HttpResponseMessage response = await client.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();
                    retVal.rawData = await response.Content.ReadAsByteArrayAsync();
                    retVal.responseAsString = await response.Content.ReadAsStringAsync();
                    retVal.headerContentType = response.Content.Headers.ContentType.ToString();
                    retVal.headerContentLength = response.Content.Headers.ContentLength;
                    success = true;
                }
                catch (Exception e)
                {
                    errorCount++;
                    System.Threading.Thread.Sleep(1000);
                }
            }

            retVal.success = success;


            return retVal;
        }
        
        public async static Task<Response> get(string url)
        {
            Response retVal = new Response();

            bool success = false;
            int errorCount = 0;

            while (!success && errorCount < RETRYCOUNT)
            {
                try
                {

                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    retVal.rawData = await response.Content.ReadAsByteArrayAsync();
                    retVal.responseAsString = await response.Content.ReadAsStringAsync();
                    retVal.headerContentType = response.Content.Headers.ContentType.ToString();
                    retVal.headerContentLength = response.Content.Headers.ContentLength;
                    success = true;
                }
                catch (Exception e)
                {
                    errorCount++;
                    System.Threading.Thread.Sleep(1000);
                }
            }

            retVal.success = success;


            return retVal;
        }

    }
}
