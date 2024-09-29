using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MyFirefoxTabs
{
    public static class HttpHelper
    {
        private static readonly HttpClient _httpClient;

        static HttpHelper()
        {
            _httpClient = new HttpClient();
        }

        public static async Task<HttpContent?> SendRequest(string url)
        {
            var resp = await _httpClient.SendAsync(BuildRequest(url)).ConfigureAwait(false);

            return resp.EnsureSuccessStatusCode().Content;
        }

        public static async Task<string> HttpContent2Text(this HttpContent content)
        {
            ArgumentNullException.ThrowIfNull(content);

            var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            byte[] buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer).ConfigureAwait(false);

            return Encoding.UTF8.GetString(buffer);
        }

        public static async Task HttpContent2File(this HttpContent content, string saveName)
        {
            ArgumentNullException.ThrowIfNull(content);

            using FileStream fileStream = new FileStream(saveName, FileMode.Create);
            await content.ReadAsStream().CopyToAsync(fileStream).ConfigureAwait(false);
        }

        public static bool FetchTopDomain(string link, out string topDomain)
        {
            topDomain = null;

            // Get domain name and locate its top-level domain.
            string domainName;
            if (!FetchDomain(link, out domainName))
                return false;


            int domainSpliterIndex = 0;
            int domainSpliterIndexPrev = 0;
            for (int i = 0; i < domainName.Length; i++)
            {
                char c = domainName[i];
                if (c == '.')
                {
                    domainSpliterIndexPrev = domainSpliterIndex;
                    domainSpliterIndex = i;
                }
            }

            topDomain = domainName.Substring(domainSpliterIndexPrev > 0 ? domainSpliterIndexPrev + 1 : domainSpliterIndexPrev);
            return true;
        }

        public static bool FetchDomain(string link, out string domain)
        {
            domain = null;

            // Get perfix length.
            int perfixLength = link.IndexOf("://");
            if (perfixLength == -1)
                return false;
            perfixLength += 3;

            // Get domian name length.
            int domainLength = link.IndexOf('/', perfixLength);
            if (domainLength == -1)
            { domainLength = link.Length - 1; }
            domainLength -= perfixLength;

            // Get domain name.
            domain = link.Substring(perfixLength, domainLength);
            return true;
        }

        private static HttpRequestMessage BuildRequest(string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:130.0) Gecko/20100101 Firefox/130.0");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/png,image/svg+xml,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US;q=0.7,en;q=0.3");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Pragma", "no-cache");

            if (FetchDomain(url, out string domain))
            { request.Headers.Add("Host", domain); }

            return request;
        }
    }
}
